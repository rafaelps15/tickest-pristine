# Tarefa: SLA completo no Tickest (v3 — por fases)

Você está no repositório do **Tickest** (.NET 10, Clean Architecture, PostgreSQL, EF Core, FluentValidation, handlers próprios de command/query com decorators via Scrutor — sem MediatR).
Este documento descreve o SLA completo, dividido em **8 fases**.

> **Execute SOMENTE a fase indicada no comando** (ex.: "Execute a Fase 1").
> Leia o documento inteiro para entender o destino final, mas não antecipe código de fases futuras.
> Ao fim de cada fase, pare e me entregue o resumo da seção "Ao terminar cada fase".

---

## Regras obrigatórias (valem para todas as fases)

1. **Não faça commit.** Deixe tudo no working tree para eu revisar no `git diff`.
2. **Leia o projeto antes de escrever código** e adapte os nomes ao que já existe. Não duplique:
   `Ticket`, enums de status/prioridade, `Entity`/`IAuditable`, domain events, `Result`/`Error`,
   abstrações de CQRS, `IApplicationDbContext`, `IDateTimeProvider`, interceptors, endpoints (`IEndpoint`),
   autorização (`Permission`, `HasPermission`), setores/departamentos, categorias, comentários, multi-tenancy.
3. **Se algo conflitar com este documento, pare e me explique antes de alterar.**
4. **Construtores tradicionais** para DI. **Não use primary constructors.**
5. **Sem exceções para regra de negócio ou validação.** Use Result pattern.
   `DbUpdateConcurrencyException` é capturada na borda e vira `Result` (409) ou log no job.
6. **Todo método assíncrono recebe e repassa `CancellationToken`.**
7. **Queries:** `.AsNoTracking()`, projeção com `.Select(...)`, paginação ou limite. Nada de `EF.Functions.*` na Application.
8. **Domínio puro:** sem `DateTime.UtcNow` no domínio; hora e calendários entram como parâmetro.
9. **Datas sempre UTC no banco**, convertidas para hora local só dentro do calendário.
10. **Multi-tenancy:** se o projeto já tiver, toda entidade de configuração nova (política, calendário, matriz) é por tenant,
    seguindo o padrão existente. Se não tiver, **não crie**; apenas me avise.
11. **Auditoria:** toda alteração de configuração de SLA e toda sobreposição manual de prioridade precisa passar pelo
    mecanismo de auditoria existente.
12. Sem comentários óbvios no código. `dotnet build` e `dotnet test` passando ao fim de cada fase.

---

## Visão geral do modelo final

```
SlaCalendar (agregado)          SlaPolicy (agregado)                 Ticket (agregado)
├─ TimeZoneId                   ├─ Name, Order, IsActive, IsDefault  ├─ Impact, Urgency, Priority
├─ Is24x7                       ├─ Conditions (setor?, categoria?)   ├─ PriorityOverride (motivo, por quem)
├─ WorkSchedule (dia → faixas)  ├─ ReopenBehavior                    ├─ SlaPlan (snapshot JSON)
└─ Holidays                     ├─ PauseOnStatuses                   ├─ Version (xmin)
                                ├─ Targets (por prioridade)          └─ SlaTimers (entidade filha, tabela própria)
PriorityMatrix (agregado)       └─ EscalationRules (até 4 níveis)        ├─ Metric, Cycle
└─ (Impact, Urgency) → Priority                                          ├─ TargetMinutes, CalendarId
                                                                         ├─ ConsumedSeconds, RunningSinceUtc
                                                                         ├─ DueOnUtc, NextActionOnUtc
                                                                         ├─ BreachedOnUtc, CompletedOnUtc, StoppedOnUtc
                                                                         └─ Escalations (níveis disparados)
```

Princípios do desenho:

- **Snapshot do plano:** na abertura, o ticket grava um `SlaPlan` (metas, calendário e regras de escalonamento que se aplicam a ele).
  Mudar a política depois **não altera** tickets em andamento. O plano só é recalculado quando o próprio ticket muda
  de prioridade, de setor ou de categoria — e o tempo já consumido é preservado.
- **Prazo materializado:** cada cronômetro grava `DueOnUtc` e `NextActionOnUtc` (próximo momento em que algo precisa acontecer:
  alerta, violação ou escalonamento). O job só consulta `NextActionOnUtc <= agora`. Nenhum cálculo de calendário no SQL.
- **Ciclos:** cada cronômetro tem `Metric` + `Cycle`. Métricas que se repetem (próxima resposta, atualização periódica,
  resolução após reabertura com novo ciclo) abrem um novo registro.

### Métricas

| Métrica | Começa | Pausa | Termina (cumprido) | Repete? |
|---|---|---|---|---|
| `FirstResponse` | abertura | não | 1º comentário público de técnico | não |
| `Resolution` | abertura | status de pausa da política | ticket resolvido | sim, se `ReopenBehavior = NewCycle` |
| `NextResponse` | comentário público do solicitante sem resposta pendente (só após a 1ª resposta) | não | comentário público de técnico | sim |
| `PeriodicUpdate` | 1º comentário público de técnico | status de pausa | cada comentário público de técnico (encerra e abre o próximo ciclo) | sim |

- Nota interna e mensagem automática **não** contam como comentário de técnico.
- Se o técnico abrir o ticket com comentário público, `FirstResponse` nasce cumprido.
- Ao resolver o ticket, ciclos abertos de `NextResponse` e `PeriodicUpdate` são **encerrados sem avaliação** (`StoppedOnUtc`)
  e não entram nos relatórios.
- Métrica sem meta na política = desativada.

### Valores padrão (seed)

**Calendário padrão** "Expediente": segunda a sexta, 08:00–18:00, America/Sao_Paulo, feriados nacionais do ano corrente e do próximo.

**Política padrão** (todas em minutos úteis do calendário padrão):

| Prioridade | 1ª resposta | Resolução | Próxima resposta | Atualização periódica |
|---|---|---|---|---|
| P1 | 30 | 240 | 60 | 120 |
| P2 | 120 | 600 | 240 | — |
| P3 | 240 | 1200 | 600 | — |
| P4 | 600 | 3000 | — | — |

**Escalonamento padrão** (resolução):

| Prioridade | Nível | Quando | Notifica |
|---|---|---|---|
| P1, P2 | 1 | 30 min úteis **antes** de vencer | técnico responsável |
| P1, P2 | 2 | no vencimento | gestor do setor |
| P1 | 3 | 60 min úteis **após** vencer | gestor do setor + administradores |

**Matriz de prioridade padrão (ITIL 3×3 → 4 níveis):**

| Impacto \ Urgência | Alta | Média | Baixa |
|---|---|---|---|
| **Alto** | P1 | P2 | P3 |
| **Médio** | P2 | P3 | P4 |
| **Baixo** | P3 | P4 | P4 |

Perguntas para o solicitante (o front exibe assim):
- Impacto: "Quem está sendo afetado?" → Alto: vários usuários/setor inteiro/segurança · Médio: um usuário · Baixo: ninguém parado.
- Urgência: "Você consegue continuar trabalhando?" → Alta: não · Média: sim, com dificuldade · Baixa: sim, normalmente.

---

## FASE 0 — Outbox (pré-requisito)

Objetivo: garantir que notificações de SLA nunca se percam se um handler falhar depois do commit.

1. Siga o padrão de Outbox do Milan (Bookify): tabela `OutboxMessages` (`Id`, `Type`, `Content`, `OccurredOnUtc`, `ProcessedOnUtc`, `Error`).
2. Substitua a publicação direta feita no `ApplicationDbContext.SaveChangesAsync` (que hoje chama o `IDomainEventsDispatcher` depois do commit) por gravação em Outbox **na mesma transação** (`SavingChanges`).
3. Serviço em segundo plano `ProcessOutboxMessagesJob` (`BackgroundService` com `PeriodicTimer`, sem Quartz) que lê em lotes, publica pelo `IDomainEventsDispatcher` existente (handlers `IDomainEventHandler<T>`, sem MediatR), marca processado e grava erro.
4. Handlers de domain event devem ser idempotentes quando enviarem notificação (registre o `OutboxMessageId` processado por handler, como no Bookify, ou explique a alternativa que usar).
5. Testes: evento gravado na mesma transação; mensagem com erro fica registrada e não trava o lote.

> Isso muda como **todo** o Tickest publica eventos. Liste no resumo todos os handlers existentes afetados.

---

## FASE 1 — Núcleo do SLA

### 1.1 Calendário

- Agregado `SlaCalendar`: `Id`, `Name`, `TimeZoneId`, `Is24x7`, `WorkSchedule` (lista de `DayOfWeek` + `Start` + `End`, como JSON),
  `Holidays` (entidade filha: `Date` como `DateOnly`, `Description`; índice único por calendário + data).
- Classe pura de domínio `BusinessCalendar` (construída a partir do `SlaCalendar`):
  - `DateTime AddBusinessMinutes(DateTime startUtc, int minutes)`
  - `DateTime SubtractBusinessMinutes(DateTime endUtc, int minutes)`
  - `int BusinessSecondsBetween(DateTime fromUtc, DateTime toUtc)`
  - percorre dia a dia em hora local, pulando dias sem faixas e feriados; limite de segurança de 366 dias.
  - `Is24x7` = aritmética simples.
- Classe pura `BrazilianNationalHolidays.For(int year)` (fixos + móveis calculados pela Páscoa: Carnaval seg/ter, Sexta-feira Santa, Corpus Christi; inclui 20/11).
- `ISlaCalendarProvider.GetAsync(Guid calendarId, CancellationToken ct)` na Application; implementação na Infrastructure com
  `HybridCache` (ou o cache já usado) por 1 hora, com invalidação quando o calendário for alterado.
- Migration com seed do calendário padrão e feriados nacionais do ano corrente e do próximo.

### 1.2 Plano e política (ainda em código nesta fase)

- Value object `SlaPlan` (gravado no `Ticket` como JSON via `ToJson()` / owned JSON):
  `PolicyId?`, `PolicyName`, `ReopenBehavior`, `PauseOnStatuses`, e por métrica: `TargetMinutes`, `CalendarId`, lista de `EscalationStep`
  (`Level`, `Trigger` = `BeforeDue` | `AfterDue`, `OffsetMinutes`, `NotifyTarget`).
- `ISlaPlanResolver.ResolveAsync(ticketContext, ct)` na Application. **Nesta fase**, a implementação devolve a política padrão
  da tabela acima a partir de código. Na Fase 4 ela passa a ler do banco — **a assinatura não muda**.

### 1.3 Cronômetro: entidade `TicketSlaTimer` (filha do agregado `Ticket`, tabela própria)

Campos: `Id`, `TicketId`, `Metric`, `Cycle`, `TargetMinutes`, `CalendarId`, `ConsumedSeconds`, `RunningSinceUtc`, `DueOnUtc`,
`NextActionOnUtc`, `BreachedOnUtc`, `CompletedOnUtc`, `StoppedOnUtc`, e coleção `Escalations` (`Level`, `FiredOnUtc`).

Calculados: `IsRunning`, `IsPaused`, `IsClosed` (completo ou parado), `IsBreached`, `Met` (completo e não violado).

Métodos (recebem `utcNow` e o `BusinessCalendar`):
- `Start`, `Pause`, `Resume`, `Retarget(newTarget, newCalendarId)`, `Complete`, `Stop`, `Reopen`.
- `ProcessDueActions(utcNow, calendar, steps)` → dispara, **em ordem**, tudo que venceu: passos `BeforeDue`, violação (no `DueOnUtc`),
  passos `AfterDue`. Cada ação gera seu domain event e é registrada em `Escalations`. Depois recalcula `NextActionOnUtc`.
- `NextActionOnUtc` = o menor entre: próximo passo `BeforeDue` não disparado (`Subtract(Due, offset)`), `DueOnUtc` se ainda não violado,
  próximo passo `AfterDue` não disparado (`Add(Due, offset)`). `null` quando pausado ou encerrado.
- Restante = `TargetMinutes * 60 - ConsumedSeconds`; se ≤ 0, vence imediatamente.
- Pausar ou retomar **não** dispara de novo níveis já disparados.

### 1.4 `Ticket`

- Campos: `SlaPlan`, coleção `SlaTimers` (o `Ticket` já tem token de concorrência sombra `Version` mapeado no `xmin` do PostgreSQL).
- Métodos (o domínio recebe `SlaCalendars`/`BusinessCalendar` e o `SlaPlan` resolvido pela Application):
  `Open`, `AddPublicComment(author: Requester|Technician, utcNow, ...)`, `ChangeStatus`, `ChangePriority`, `Resolve`, `Reopen`,
  `ReplanSla(newPlan, utcNow, calendars)`, `ProcessSlaDueActions(utcNow, calendars)`.
- Nesta fase ative `FirstResponse` e `Resolution`. `NextResponse` e `PeriodicUpdate` ficam para a Fase 6 (o enum já existe).
- Domain events: `TicketSlaBreachedDomainEvent(TicketId, Metric, Cycle)`, `TicketSlaEscalatedDomainEvent(TicketId, Metric, Cycle, Level, NotifyTarget)`,
  `TicketResolvedDomainEvent(TicketId, ResolutionMet)`.
- Erros em `TicketErrors`: `NotFound`, `AlreadyResolved`, `NotResolved`, `SlaNotRunning`, `SlaNotPaused`, `ConcurrencyConflict`.

### 1.5 Persistência

- `TicketSlaTimers` com índice filtrado em `NextActionOnUtc` (`WHERE NextActionOnUtc IS NOT NULL`), incluindo `TicketId`.
- Índice em `(TicketId, Metric, Cycle)` único.
- Value converters UTC para `DateTime`/`DateTime?` em `ConfigureConventions`.
- Migration; para tickets já existentes e abertos, crie os cronômetros a partir da data de criação de auditoria. Mostre o SQL.

### 1.6 Job `SlaMonitorJob`

- `BackgroundService` com `PeriodicTimer` (sem Quartz), sem execuções sobrepostas, a cada 60 s, um único `now` por execução.
- Lotes de 200 `TicketId` distintos com `NextActionOnUtc <= now` (ordenados), até 50 lotes.
- Carrega os tickets com os cronômetros abertos (`Include` filtrado) e chama `ProcessSlaDueActions`.
- `SaveChanges` com tratamento de conflito: desanexa só as entradas em conflito e salva de novo (limite = tamanho do lote).
- `ChangeTracker.Clear()` entre lotes. Log só com totais.
- Se a API rodar em mais de uma instância, garanta uma execução por vez com advisory lock do PostgreSQL (`pg_try_advisory_lock`). Se for uma só, não faça; me diga qual é o caso.

### 1.7 Application e endpoints

- Handlers: obtêm plano e calendários, usam `UtcNow` uma vez, convertem conflito em `TicketErrors.ConcurrencyConflict` (409).
- `GetTicketSlaStatusQuery(ticketId)`: cronômetros do ticket com minutos úteis restantes (calculado em memória).
- `GetTicketsAtRiskQuery(windowMinutes 1–1440)`: até 500, ordenado por vencimento.
- Endpoints com `CancellationToken` e autorização existente.

### 1.8 Testes da Fase 1

Hora local America/Sao_Paulo, calendário padrão, feriados 12/10/2026 (seg) e 20/11/2026 (sex) cadastrados.

| Cenário | Esperado |
|---|---|
| sex 16/10/2026 17:59 + 30 min úteis | seg 19/10/2026 08:29 |
| sex 09/10/2026 17:59 + 30 min (12/10 feriado) | ter 13/10/2026 08:29 |
| sex 16/10/2026 17:00 + 240 min | seg 19/10/2026 11:00 |
| sáb 17/10/2026 10:00 + 120 min | seg 19/10/2026 10:00 |
| qui 19/11/2026 14:00 + 1200 min (20/11 feriado) | ter 24/11/2026 14:00 |
| subtrair 30 min de seg 19/10/2026 08:10 | sex 16/10/2026 17:40 |
| segundos úteis entre sex 16/10 17:00 e seg 19/10 09:00 | 7200 |
| calendário 24x7, sex 22:00 + 240 min | sáb 02:00 |
| `BrazilianNationalHolidays.For(2026)` | inclui Carnaval 16 e 17/02, Sexta Santa 03/04, Corpus Christi 04/06, 20/11 |
| P1 aberto sex 16/10 17:59 | 1ª resposta vence seg 08:29; resolução seg 11:59; nível 1 seg 11:29 |
| P2 aberto seg 19/10 09:00, pausa 11:00, retoma ter 10:00 | consumido 7200 s; resolução vence ter 20/10 18:00 |
| pausado | `DueOnUtc` e `NextActionOnUtc` nulos |
| P4 aberto seg 08:00, vira P1 às 12:00 | resolução vence 12:00; job viola e dispara nível 2 |
| job atrasado: nível 1 e vencimento já passados | dispara nível 1, depois violação + nível 2, em ordem |
| pausa/retomada após nível 1 | nível 1 não é disparado de novo |
| resolvido antes do prazo | `ResolutionMet = true` |
| resolvido após vencer | continua violado, `Met = false` |
| reabertura com `Continue` | mesmo ciclo volta a correr do consumo anterior |
| comentário público de técnico | 1ª resposta cumprida; nota interna não cumpre |
| conflito de concorrência no handler | retorna `ConcurrencyConflict` |

---

## FASE 2 — Matriz impacto × urgência

- Enums `TicketImpact` e `TicketUrgency` (`High`, `Medium`, `Low`).
- Agregado `PriorityMatrix` com as 9 combinações (seed = tabela padrão). Validação: todas as 9 preenchidas.
- `Ticket`: `Impact`, `Urgency`, `Priority` calculada pela matriz na abertura e quando impacto/urgência mudam.
- **Sobreposição manual** por técnico: `OverridePriority(priority, reason, technicianId, utcNow)`; motivo obrigatório (mín. 10 caracteres),
  gravado (`PriorityOverriddenBy`, `PriorityOverrideReason`, `PriorityOverriddenOnUtc`), auditado, e dispara `ReplanSla`.
- Permissão nova `OverrideTicketPriority`.
- Endpoints: ler/alterar matriz (permissão `ManageSla`), sobrepor prioridade.
- Migration: tickets existentes recebem impacto/urgência que reproduzem a prioridade atual.
- Testes: as 9 combinações; sobreposição sem motivo falha; sobreposição recalcula o SLA mantendo o consumo.

---

## FASE 3 — Calendários por setor

- Setor/equipe existente ganha `SlaCalendarId?` (nulo = calendário padrão).
- Regra de escolha do calendário de uma métrica: calendário da meta da política → calendário do setor responsável → calendário padrão.
- Mudança de setor do ticket dispara `ReplanSla` (consumo preservado, prazo recalculado no novo calendário).
- Endpoints de administração (permissão `ManageSla`): CRUD de calendários, faixas por dia, feriados, e
  **importar feriados nacionais de um ano** (`BrazilianNationalHolidays`). Não permitir excluir calendário em uso.
- Invalidação de cache ao alterar calendário.
- Testes: setor com sábado 08:00–12:00; troca de setor no meio do prazo; calendário com almoço (08–12 e 13–18).

---

## FASE 4 — Políticas configuráveis

- Agregado `SlaPolicy`: `Name`, `Order`, `IsActive`, `IsDefault`, `Conditions` (`DepartmentId?`, `CategoryId?` — use as entidades existentes),
  `ReopenBehavior` (`Continue` | `NewCycle`), `PauseOnStatuses`, `Targets` (por prioridade: as 4 métricas em minutos, nulo = desativada,
  `CalendarId?`), `EscalationRules` (Fase 5 completa as regras; aqui só a estrutura).
- Invariantes: exatamente **uma** política padrão ativa; padrão sem condições; metas de `FirstResponse` e `Resolution` obrigatórias
  para as 4 prioridades; valores > 0.
- `ISlaPlanResolver` passa a ler do banco: primeira política ativa, por `Order`, cujas condições casam com o ticket; senão a padrão.
- Migration: seed da política padrão com os valores da tabela.
- Endpoints de administração (permissão `ManageSla`): CRUD, ativar/desativar, reordenar, pré-visualizar "qual política se aplica a este ticket".
- Se houver front-end no repositório, crie as telas de administração seguindo o padrão existente. Se não houver, só API — me avise.
- Testes: escolha por setor, por categoria, por ordem; fallback para a padrão; alteração da política não muda ticket já aberto;
  `NewCycle` na reabertura cria ciclo 2 com meta cheia.

---

## FASE 5 — Escalonamento multinível

- `EscalationRule`: `Metric`, `Priority?` (nulo = todas), `Level` (1–4), `Trigger` (`BeforeDue` | `AfterDue`), `OffsetMinutes`,
  `NotifyTarget` (`AssignedTechnician`, `DepartmentManager`, `Role`, `SpecificUser`), `RoleId?`/`UserId?`.
- Invariantes: no máximo 4 níveis por métrica + prioridade; níveis únicos; ordem temporal crescente com o nível.
- Seed com a tabela de escalonamento padrão.
- Handler de `TicketSlaEscalatedDomainEvent` resolve os destinatários e notifica pelo mecanismo existente
  (idempotente via Outbox). Se não houver mecanismo de notificação, crie a interface `ISlaNotifier` com implementação que só registra log e me avise.
- Testes: 3 níveis disparando em ordem; job atrasado dispara todos os vencidos de uma vez, em ordem; regra por prioridade específica vence a genérica.

---

## FASE 6 — Próxima resposta e atualização periódica

- Ative `NextResponse` e `PeriodicUpdate` conforme a tabela de métricas.
- `NextResponse`: só abre ciclo se a 1ª resposta já foi cumprida e não houver ciclo aberto; vários comentários do solicitante seguidos não abrem vários ciclos.
- `PeriodicUpdate`: cada comentário público de técnico encerra o ciclo atual e abre o próximo; pausa com os status de pausa.
- Ao resolver: ciclos abertos dessas métricas são encerrados com `StoppedOnUtc`.
- Escalonamento também vale para essas métricas, se houver regra.
- Testes: sequência solicitante → técnico → solicitante → solicitante → técnico gera 2 ciclos de `NextResponse`;
  atualização periódica violada após 2h úteis sem comentário de técnico; pausa congela a atualização periódica; resolução encerra sem avaliar.

---

## FASE 7 — Relatórios de cumprimento

Todas são queries de leitura (`AsNoTracking`, projeção, paginação), permissão `ViewSlaReports`, filtros por período (data de abertura),
prioridade, setor, categoria, técnico e métrica. Considere só ciclos `Completed` (ciclos `Stopped` ficam fora).

1. **Resumo de cumprimento:** por métrica e prioridade — total, cumpridos, violados, % cumprido.
2. **Tempo médio** (em horas úteis, a partir de `ConsumedSeconds`) de 1ª resposta e de resolução, por prioridade.
3. **Ranking** por técnico e por setor: % cumprido e quantidade de violações.
4. **Lista de violações** paginada: ticket, métrica, ciclo, prazo, quando violou, último nível de escalonamento disparado.
5. **Tickets abertos agora** por situação: no prazo, em risco (nível 1 disparado), violados, pausados.
6. **Sobreposições de prioridade** no período: quem, quando, de/para, motivo.
7. Exportação **CSV** dos relatórios 1, 3, 4 e 6 (streaming, sem carregar tudo em memória).

Índices necessários: crie apenas os que os planos de execução mostrarem necessários; me mostre os planos das queries principais.

Testes: cálculo de % com ciclos cumpridos, violados e parados; filtro por período; paginação.

---

## Ao terminar cada fase

1. `dotnet build` sem erros nem warnings novos; `dotnet test` passando.
2. **Não faça commit.**
3. Me entregue:
   - fase executada e arquivos criados/alterados;
   - adaptações feitas em relação a este documento e o motivo;
   - conflitos encontrados com o código existente;
   - decisões que você precisou tomar sozinho;
   - comando para aplicar a migration;
   - o que a próxima fase vai precisar do que foi feito agora.
