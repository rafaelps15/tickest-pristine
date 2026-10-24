---
id: 004
titulo: SLA — calendário útil e feriados nacionais
status: Rascunho        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em:
branch:
---

# 004 — SLA: calendário útil e feriados nacionais

## 1. Contexto
- **Demanda:** BL-004 (identificador anterior; desde a spec 007, a demanda é identificada pela própria spec).
- **Fonte:** `specs/_fontes/prompt-sla.md`, cópia byte a byte do `prompt-sla-tickest.md` (não versionado, mantido na raiz), feita em 2026-10-23 (SHA-256 `07ecd5f66563fc292fbdcf29c076f6f7ce43e79df683fe1e365ecca3975bd57f` nos dois arquivos). Os itens citados nesta spec (1.1, 1.8, regras 10 e 11) estão nessa cópia.
- **Pedido original:** `/spec-new` com o `prompt-sla-tickest.md` (não versionado) como pedido, escolhido no chat em 2026-10-23: "SLA, Fase 1". Por decisão no chat (DEC-1), a Fase 1 foi dividida em três specs; esta cobre o item **1.1 Calendário** do prompt:
  > Agregado `SlaCalendar`: `Id`, `Name`, `TimeZoneId`, `Is24x7`, `WorkSchedule` (lista de `DayOfWeek` + `Start` + `End`, como JSON), `Holidays` (entidade filha: `Date` como `DateOnly`, `Description`; índice único por calendário + data). Classe pura de domínio `BusinessCalendar` (construída a partir do `SlaCalendar`): `AddBusinessMinutes`, `SubtractBusinessMinutes`, `BusinessSecondsBetween`; percorre dia a dia em hora local, pulando dias sem faixas e feriados; limite de segurança de 366 dias. `Is24x7` = aritmética simples. Classe pura `BrazilianNationalHolidays.For(int year)` (fixos + móveis calculados pela Páscoa: Carnaval seg/ter, Sexta-feira Santa, Corpus Christi; inclui 20/11). `ISlaCalendarProvider.GetAsync(Guid calendarId, CancellationToken ct)` na Application; implementação na Infrastructure com `HybridCache` (ou o cache já usado) por 1 hora, com invalidação quando o calendário for alterado. Migration com seed do calendário padrão e feriados nacionais do ano corrente e do próximo.

  Valor padrão do prompt: calendário "Expediente", segunda a sexta, 08:00–18:00, `America/Sao_Paulo`, feriados nacionais. Cenários de teste do prompt (1.8) que cabem nesta spec: as oito primeiras linhas da tabela e a de `BrazilianNationalHolidays.For(2026)`.

  Ajuste pedido no chat por Rafael Santos (2026-10-23): "Não é pra ter seed para SLA"; depois, "sim, padrão em código". A migration do prompt deixa de gravar dados, e o calendário padrão passa a ser definido em código (DEC-5, DEC-7).
- **Série da Fase 1:** 004 (esta) → 005 (plano de SLA, cronômetros de 1ª resposta e resolução, migração dos chamados existentes e consultas; itens 1.2 a 1.5 e 1.7) → 006 (job de monitoramento e escalonamento; item 1.6). As decisões DEC-1 a DEC-4 e DEC-7 valem para a série e serão repetidas nas specs 005 e 006.
- **Referências:**
  - `SPEC-TICKEST.md` (documento de produto): esta spec atende o **RF-50** (calendários de expediente, faixas por dia, 24x7, feriados nacionais) e o **RF-51** (calendário padrão "Expediente" em código). Os demais requisitos do SLA útil (RF-52, RF-53, RF-65, RF-66, RN-37 a RN-44) ficam para as specs 005 e 006.
  - `SYSTEM_SPECIFICATION.md`: 1.2 (SLA completo "planejado, não implementado"), 2.6 (RN-01 a RN-03, SLA atual em horas corridas), 2.7 (C-14, C-15), 9.5 (Q-10 e Q-11).
  - demais fases do SLA: BL-012 a BL-018 (identificadores anteriores), fora do repositório desde a spec 007.
  - Código: `src/TickestPristine.Application/Tickets/TicketSla.cs` (SLA atual, intocado nesta spec); `src/TickestPristine.Infrastructure/Authorization/PermissionProvider.cs` (único uso de `HybridCache`, modelo para o provider); `src/TickestPristine.Application/Abstractions/Authorization/IPermissionProvider.cs` (modelo da interface, com `InvalidateAsync`); `src/TickestPristine.Infrastructure/Audit/AuditLoggingInterceptor.cs` (auditoria de entidades `IAuditable`); `src/TickestPristine.Domain/Departments/Department.cs` e `Infrastructure/Departments/DepartmentConfiguration.cs` (referência viva de entidade).
  - Testes de referência: `tests/TickestPristine.IntegrationTests/Database/RefreshTokenStorageTests.cs` (teste de integração que usa o `DbContext` direto, sem HTTP).
- **Fatos do repositório que afetam o prompt:**
  - Não há multi-tenancy: o calendário é global, e nenhuma estrutura de tenant é criada (regra 10 do prompt pede só o aviso).
  - O projeto usa domínio anêmico e construtores primários (`CLAUDE.md`, regras permanentes); o prompt pede o contrário. Resolvido pela DEC-2.
  - O fuso `America/Sao_Paulo` já é o fuso fixo dos relatórios (`GetTicketVolumeQueryHandler.cs:26`); o Brasil não tem horário de verão desde 2019.
  - O .NET não tem calendário de expediente nem de feriados: `GregorianCalendar` e `TimeZoneInfo` cobrem só datas civis e fusos. Por isso o cálculo útil e os feriados são código próprio, sem dependência externa.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | O banco guarda calendários de SLA com nome, fuso (identificador IANA), indicador 24x7 e grade semanal de expediente (lista de faixas dia da semana + início + fim, gravada como JSON; um dia pode ter mais de uma faixa ou nenhuma). Os feriados ficam numa tabela filha (data e descrição), com no máximo um feriado por calendário e data. | pedido (1.1) |
| REQ-2 | O calendário padrão "Expediente" (segunda a sexta, 08:00–18:00, `America/Sao_Paulo`, não 24x7, feriados de `BrazilianNationalHolidays` para qualquer ano) é definido em código, com um Id fixo reservado, e não existe no banco. A migration desta spec cria só a estrutura, sem gravar dados; nenhuma migration do SLA insere configuração padrão (DEC-7). | pedido (valores padrão); DEC-5; DEC-7 |
| REQ-3 | Somar minutos úteis a um instante UTC devolve o instante UTC em que o último minuto útil é consumido, contando só o tempo dentro das faixas do dia em hora local e pulando dias sem faixa e feriados. Um início fora do expediente começa a contar na próxima faixa. Se o último minuto terminar exatamente no fim de uma faixa, o resultado é esse fim de faixa. | pedido (1.1, 1.8); DEC-9 |
| REQ-4 | Subtrair minutos úteis de um instante UTC é a operação inversa: devolve o instante em que, contando para a frente, faltariam exatamente esses minutos úteis até o instante dado. | pedido (1.1, 1.8); DEC-9 |
| REQ-5 | O cálculo de segundos úteis entre dois instantes UTC conta só o tempo dentro das faixas, sem feriados; se o fim não for posterior ao início, o resultado é zero. | pedido (1.1, 1.8) |
| REQ-6 | Num calendário 24x7, as três operações usam aritmética corrida, sem grade nem feriados. | pedido (1.1, 1.8) |
| REQ-7 | Os cálculos percorrem no máximo 366 dias. Ao passar do limite (ex.: calendário sem nenhuma faixa), lançam `InvalidOperationException`: calendário mal configurado é falha inesperada. | pedido (1.1); DEC-8 |
| REQ-8 | `BrazilianNationalHolidays.For(year)` devolve os feriados do ano: os fixos (01/01, 21/04, 01/05, 07/09, 12/10, 02/11, 15/11, 20/11, 25/12) e os móveis calculados pela Páscoa (Carnaval segunda e terça, Sexta-feira Santa, Corpus Christi), cada um com descrição em pt-BR. | pedido (1.1); Leis 662/1949, 6.802/1980 e 14.759/2023 para os fixos |
| REQ-9 | `ISlaCalendarProvider.GetAsync(calendarId)` devolve o calendário pronto para os cálculos: para o Id do padrão, o calendário em código, sem ir ao banco; para os demais, o do banco, com grade e feriados. Calendário inexistente devolve falha `SlaCalendarErrors.NotFound`, sem exceção. | pedido (1.1); DEC-5; `CLAUDE.md` (padrão Result, `{Entity}Errors`) |
| REQ-10 | O provider guarda os calendários do banco em cache por 1 hora e expõe `InvalidateAsync(calendarId)`, que faz a próxima leitura buscar de novo no banco. | pedido (1.1); DEC-6 |
| REQ-11 | Inclusão, alteração e exclusão de calendário e de feriado no banco passam pela auditoria existente. | pedido (regra 11) |
| REQ-12 | Nada muda no comportamento atual dos chamados nem no SLA de resolução atual: as specs 005 e 006 é que ligam o calendário aos chamados. | DEC-1 |

## 3. Critérios de aceitação
Horários em hora local de `America/Sao_Paulo` (UTC−3); os testes convertem para UTC na entrada e na saída. "Calendário padrão" = o calendário em código do REQ-2; 12/10/2026 e 20/11/2026 são feriados nacionais e já fazem parte dele.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-3 | o calendário padrão | soma 30 minutos úteis a sex 16/10/2026 17:59 | seg 19/10/2026 08:29 |
| AC-2 | REQ-3 | o calendário padrão (12/10/2026, segunda, é feriado) | soma 30 minutos úteis a sex 09/10/2026 17:59 | ter 13/10/2026 08:29 |
| AC-3 | REQ-3 | o calendário padrão | soma 240 minutos úteis a sex 16/10/2026 17:00 | seg 19/10/2026 11:00 |
| AC-4 | REQ-3 | o calendário padrão | soma 120 minutos úteis a sáb 17/10/2026 10:00 | seg 19/10/2026 10:00 |
| AC-5 | REQ-3 | o calendário padrão (20/11/2026, sexta, é feriado) | soma 1200 minutos úteis a qui 19/11/2026 14:00 | ter 24/11/2026 14:00 |
| AC-6 | REQ-3 | o calendário padrão | soma 60 minutos úteis a sex 16/10/2026 17:00 | sex 16/10/2026 18:00 (fim de faixa, DEC-9) |
| AC-7 | REQ-4 | o calendário padrão | subtrai 30 minutos úteis de seg 19/10/2026 08:10 | sex 16/10/2026 17:40 |
| AC-8 | REQ-4 | o calendário padrão | subtrai 10 minutos úteis de seg 19/10/2026 08:10 | seg 19/10/2026 08:00 (início de faixa, DEC-9) |
| AC-9 | REQ-5 | o calendário padrão | conta os segundos úteis entre sex 16/10/2026 17:00 e seg 19/10/2026 09:00; e entre seg 19/10/2026 09:00 e sex 16/10/2026 17:00 | 7200; e 0 |
| AC-10 | REQ-6 | um calendário 24x7 com feriado em 17/10/2026 | soma 240 minutos a sex 16/10/2026 22:00; subtrai 240 minutos de sáb 17/10/2026 02:00; conta os segundos entre os dois | sáb 17/10/2026 02:00; sex 16/10/2026 22:00; 14400 |
| AC-11 | REQ-7 | um calendário não 24x7 sem nenhuma faixa | soma 1 minuto útil a qualquer instante | lança `InvalidOperationException` (DEC-8) |
| AC-12 | REQ-8 | — | `BrazilianNationalHolidays.For(2026)` | 13 datas: as 9 fixas do REQ-8 em 2026 mais 16/02 e 17/02 (Carnaval), 03/04 (Sexta-feira Santa) e 04/06 (Corpus Christi), todas com descrição |
| AC-13 | REQ-8 | — | `BrazilianNationalHolidays.For(2027)` | 13 datas, com os móveis em 08/02, 09/02, 26/03 e 27/05 |
| AC-14 | REQ-1, REQ-2 | o banco migrado | lê as tabelas de calendário e de feriado | as duas existem e estão vazias |
| AC-15 | REQ-1 | um calendário gravado no banco com um feriado | grava um segundo feriado na mesma data do mesmo calendário | o `SaveChangesAsync` falha por violação de unicidade; a mesma data em outro calendário grava |
| AC-16 | REQ-2, REQ-9 | o banco migrado, sem nenhum calendário | `GetAsync(SlaCalendarIds.Default)`; soma 30 minutos úteis a sex 09/10/2026 17:59 e a ter 24/12/2030 17:59 | sucesso; ter 13/10/2026 08:29 e qui 26/12/2030 08:29 (o feriado de 25/12/2030 vem do cálculo, sem dado no banco) |
| AC-17 | REQ-9 | o banco migrado | `GetAsync` de um id inexistente | falha com o código `SlaCalendars.NotFound`, tipo `NotFound` e mensagem em pt-BR |
| AC-18 | REQ-9, REQ-10 | um calendário gravado no banco (sábado 08:00–12:00, `America/Sao_Paulo`), já lido uma vez pelo provider | a primeira leitura soma 60 minutos úteis a sáb 17/10/2026 11:30; depois um feriado em 24/10/2026 é gravado direto no banco e o provider é lido de novo; depois `InvalidateAsync` e nova leitura, somando 60 minutos úteis a sáb 17/10/2026 11:30 nas duas | a primeira e a segunda leitura dão sáb 24/10/2026 08:30 (a segunda ainda sem o feriado, do cache); a terceira dá sáb 31/10/2026 08:30 |
| AC-19 | REQ-11 | o banco migrado | inclui um calendário com um feriado e depois altera o nome dele | a auditoria tem os registros de inclusão do calendário e do feriado e o de alteração do calendário |
| AC-20 | REQ-12 | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam, inclusive os do SLA atual (`TicketSlaTests`, `GetTicketsQueryHandlerTests`, `TicketsTests`) sem alteração |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como dividir a Fase 1 do prompt em specs? | (a) três specs: calendário; plano, cronômetros e consultas; job; (b) uma spec só; (c) duas specs | (a) três specs: 004, 005 e 006 | Rafael Santos, 2026-10-23, no chat |
| DEC-2 | Estilo de código: o do prompt (métodos no domínio, sem construtores primários) ou o do `CLAUDE.md`? | (a) `CLAUDE.md`: entidades só com propriedades, regras em classes da Application, construtores primários; (b) o do prompt só no código de SLA | (a) `CLAUDE.md`. Nesta spec, `BusinessCalendar`, `BrazilianNationalHolidays` e o calendário padrão ficam na Application (`Application/SlaCalendars/`), e não no Domain | Rafael Santos, 2026-10-23, no chat |
| DEC-3 | O que acontece com o SLA atual (horas corridas, `DueAtUtc`, `SlaWarningAtUtc`, situação, filtros e painel)? | (a) manter campos e contrato, alimentados pelo cronômetro de resolução; (b) substituir; (c) manter em paralelo | (a). Aplica-se na spec 005; nesta, nada muda (REQ-12) | Rafael Santos, 2026-10-23, no chat |
| DEC-4 | O Outbox (Fase 0) não existe. Como seguir? | (a) seguir sem Outbox: a Fase 1 só dispara eventos, sem handler que notifique; o Outbox vira pré-requisito da Fase 5; (b) fazer a Fase 0 antes | (a). Sem efeito nesta spec, que não dispara eventos | Rafael Santos, 2026-10-23, no chat |
| DEC-5 | De onde vem e como se reconhece o calendário padrão? | (a) em código, com Id fixo reservado (`SlaCalendarIds.Default`), sem linha no banco; (b) no `appsettings`; (c) só no banco, com o cadastro de calendários trazido para esta spec | (a) em código, com Id fixo reservado. Na Fase 3, o cadastro não pode usar esse Id; calendários cadastrados substituem o padrão por setor | Rafael Santos, 2026-10-23, no chat ("sim, padrão em código") |
| DEC-6 | Cache do calendário: o `CLAUDE.md` restringe o `HybridCache` ao `PermissionProvider` sem decisão explícita; o prompt pede cache de 1 hora. | (a) `HybridCache` por 1 hora, com `InvalidateAsync`, no mesmo modelo do `PermissionProvider` (a invalidação só ganha quem chame na Fase 3); (b) sem cache: cada leitura vai ao banco | (a) `HybridCache` por 1 hora, com `InvalidateAsync`, para os calendários do banco; o padrão em código não precisa de cache | Rafael Santos, 2026-10-23, no chat |
| DEC-7 | O prompt pede seed do calendário padrão e dos feriados. Há seed? | (a) sem seed: nenhuma migration do SLA grava dados; (b) seed na migration; (c) semeador na inicialização | (a) sem seed, para todo o SLA. **Esclarecimento do alcance (2026-10-23):** "sem seed" proíbe a inserção automática de dados de configuração padrão (calendários, políticas, matrizes e regras de escalonamento); não proíbe migrations de dados de negócio existentes. A migração dos chamados já existentes, inclusive a inicialização dos dados de SLA deles, pode ser especificada na fase correspondente, com os requisitos e critérios dessa fase | Rafael Santos, 2026-10-23, no chat ("Não é pra ter seed para SLA"); esclarecimento por Rafael Santos, 2026-10-23, no chat |
| DEC-8 | O que acontece quando o cálculo passa do limite de 366 dias? | (a) exceção `InvalidOperationException` (calendário mal configurado é falha inesperada; vai para o `GlobalExceptionHandler` ou para o log do job); (b) `Result` com falha `SlaCalendarErrors.NoBusinessTime`, propagado por quem chama | (a) exceção `InvalidOperationException`; a Fase 3 impede, por validação, salvar calendário sem faixa | Rafael Santos, 2026-10-23, no chat |
| DEC-9 | Fronteira das faixas: quando o último minuto termina exatamente no fim da faixa (ou, na subtração, no início), o resultado é a borda da faixa ou a borda da próxima faixa (ex.: sexta 18:00 ou segunda 08:00)? | (a) a borda da própria faixa (sexta 18:00; segunda 08:00 na subtração); (b) a próxima faixa (segunda 08:00; sexta 18:00 na subtração) | (a) a borda da própria faixa: o prazo vence no instante em que o tempo acaba | Rafael Santos, 2026-10-23, no chat |

## 5. Fora do escopo
- Plano de SLA, cronômetros, mudanças em `Ticket`, migração dos chamados existentes, consultas de SLA (spec 005).
- Job de monitoramento, escalonamento e eventos de violação (spec 006).
- Endpoints de calendário (CRUD, faixas, feriados, importação de feriados) e calendário por setor (Fase 3). Nesta spec não há rota nova nem permissão nova; os calendários do banco só são gravados pelos testes.
- Alterar o calendário padrão sem novo deploy (fica em código, DEC-5).
- Validação da grade (faixas sobrepostas, fim antes do início): entra com os endpoints da Fase 3.
- Feriados estaduais e municipais.
- Conversores UTC em `ConfigureConventions` (item 1.5 do prompt, spec 005).
- Outbox (DEC-4) e multi-tenancy (não existe no projeto).

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Domain, `src/TickestPristine.Domain/SlaCalendars/`: `SlaCalendar.cs` (`Entity`, `IAuditable`; `Id`, `Name`, `TimeZoneId`, `Is24x7`, `WorkSchedule`), `SlaWorkPeriod.cs` (`DayOfWeek`, `Start` e `End` como `TimeOnly`), `SlaCalendarHoliday.cs` (`Entity`, `IAuditable`; `Id`, `SlaCalendarId`, `Date` como `DateOnly`, `Description`), `SlaCalendarErrors.cs` (`NotFound`).
  - Application, `src/TickestPristine.Application/SlaCalendars/`:
    - `BusinessCalendar.cs`: classe imutável construída com a grade, o fuso, o indicador 24x7 e uma fonte de feriados por ano (`Func<int, IReadOnlySet<DateOnly>>`), para servir tanto a lista fixa do banco quanto o cálculo para qualquer ano do padrão; `AddBusinessMinutes`, `SubtractBusinessMinutes`, `BusinessSecondsBetween`.
    - `BrazilianNationalHolidays.cs`: estática; Páscoa pelo algoritmo de Meeus/Jones/Butcher.
    - `DefaultSlaCalendar.cs`: grade "Expediente", fuso e `Create()` que monta o `BusinessCalendar` com os feriados de `BrazilianNationalHolidays`.
    - `SlaCalendarIds.cs`: `Default`, o Id fixo reservado.
  - `src/TickestPristine.Application/Abstractions/Sla/ISlaCalendarProvider.cs` (`GetAsync` → `Result<BusinessCalendar>`, `InvalidateAsync`). `IApplicationDbContext` ganha `SlaCalendars` e `SlaCalendarHolidays`.
  - Infrastructure, `src/TickestPristine.Infrastructure/SlaCalendars/`: `SlaCalendarConfiguration.cs` (tabela `sla_calendars`; `WorkSchedule` como `jsonb`), `SlaCalendarHolidayConfiguration.cs` (tabela `sla_calendar_holidays`; FK com exclusão em cascata; índice único `(sla_calendar_id, date)`), `SlaCalendarProvider.cs` (Id padrão → `DefaultSlaCalendar`; demais → banco com `HybridCache`) e `SlaCalendarCacheKeys.cs`, no modelo de `PermissionProvider`; registro na DI; `ApplicationDbContext` com os dois `DbSet`.
  - Migration `Add_Sla_Calendars` em `Database/Migrations/`, só com estrutura (sem `HasData`).
  - Testes: `tests/TickestPristine.Application.UnitTests/SlaCalendars/BusinessCalendarTests.cs` (AC-1 a AC-11) e `BrazilianNationalHolidaysTests.cs` (AC-12, AC-13); `tests/TickestPristine.IntegrationTests/SlaCalendars/SlaCalendarsTests.cs` (AC-14 a AC-19, pelo `DbContext` e pelo provider resolvidos do `Services`, como em `RefreshTokenStorageTests`).
- **Contratos** (rotas, payloads, erros, eventos): nenhuma rota nova. Erro novo `SlaCalendars.NotFound` (tipo `NotFound`, "O calendário de SLA com o Id = '{id}' não foi encontrado"). Nenhum evento de domínio.
- **Mudança no banco:** sim. Tabelas `sla_calendars` e `sla_calendar_holidays` e índice único, vazias. Nenhuma tabela existente muda.
- **Riscos:**
  - R-1: Carnaval e Corpus Christi são pontos facultativos nacionais, não feriados legais. Entram no padrão porque o prompt pede; tirá-los exige mudar o código até a Fase 3.
  - R-2: mudar a grade ou os feriados do padrão exige novo deploy (DEC-5).
  - R-3: o fuso é lido por `TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")`; no Windows, o .NET converte o id IANA pelo ICU. Se o ICU não estiver disponível (modo invariante de globalização), a busca falha. Os testes rodam no Windows local e no Linux do CI e do container, o que expõe o problema se ele existir.
  - R-4: a grade é JSON; mudança de formato nas Fases 3 e seguintes exige migration de dados.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Entidades `SlaCalendar`, `SlaCalendarHoliday`, `SlaWorkPeriod`, `SlaCalendarErrors`; configurações EF com `jsonb` e índice único, sem seed; `DbSet` no `IApplicationDbContext`/`ApplicationDbContext`; migration `Add_Sla_Calendars` | AC-14, AC-15, AC-19 | add-entity | [ ] |
| T-2 | `BrazilianNationalHolidays` (fixos e móveis pela Páscoa) | AC-12, AC-13 | add-feature | [ ] |
| T-3 | `BusinessCalendar`: soma, subtração e segundos úteis em hora local, 24x7, fonte de feriados por ano, limite de 366 dias (DEC-8) e fronteiras (DEC-9) | AC-1 a AC-11 | add-feature | [ ] |
| T-4 | `SlaCalendarIds` e `DefaultSlaCalendar`; `ISlaCalendarProvider` e `SlaCalendarProvider` (padrão em código; banco com `HybridCache` de 1 hora e `InvalidateAsync`); registro na DI | AC-1 a AC-9, AC-16, AC-17, AC-18 | add-feature | [ ] |
| T-5 | Testes unitários de `BusinessCalendar` e `BrazilianNationalHolidays`; testes de integração de tabelas vazias, unicidade, provider (padrão e banco), cache e auditoria | AC-1 a AC-19 | add-tests | [ ] |
| T-6 | Build e testes da solução, conferindo que nenhum teste existente do SLA atual mudou | AC-20 | add-tests | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | | | |
| AC-3 | | | |
| AC-4 | | | |
| AC-5 | | | |
| AC-6 | | | |
| AC-7 | | | |
| AC-8 | | | |
| AC-9 | | | |
| AC-10 | | | |
| AC-11 | | | |
| AC-12 | | | |
| AC-13 | | | |
| AC-14 | | | |
| AC-15 | | | |
| AC-16 | | | |
| AC-17 | | | |
| AC-18 | | | |
| AC-19 | | | |
| AC-20 | | | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:** a atualizar em `SYSTEM_SPECIFICATION.md`: 1.2 (SLA completo passa a "em implementação": calendário útil pronto), 2.4 (entidades `SlaCalendar` e `SlaCalendarHoliday`), 2.6 (regras de cálculo útil, REQ-3 a REQ-7, e calendário padrão em código), 2.7 (C-15), 3.8 (tabelas novas), 4.5 (erro `SlaCalendars.NotFound`), 8 (evidências) e 9.5 (Q-10, já respondida em parte em 2026-10-23 com a criação do backlog: completar com as DEC-1 a DEC-4 e DEC-7 da série, sem reescrever o registro existente).
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-23 | Rascunho | Spec criada pela `spec-new` a partir do item 1.1 do `prompt-sla-tickest.md`. DEC-1 a DEC-4 decididas por Rafael Santos no chat antes da escrita; DEC-5 (Id fixo) e DEC-6 (`HybridCache`) decididas logo depois; DEC-7 a DEC-9 em aberto. |
| 2026-10-23 | Rascunho | Revisão pedida por Rafael Santos no chat ("Não é pra ter seed para SLA"; "sim, padrão em código"): DEC-7 decidida (sem seed, para todo o SLA); DEC-5 refeita (padrão em código, com Id fixo reservado); REQ-2, REQ-9, REQ-10, AC-14, AC-16 e AC-18 reescritos; fonte de feriados por ano no `BusinessCalendar`; riscos R-1 e R-2 refeitos. DEC-8 e DEC-9 em aberto. |
| 2026-10-23 | Rascunho | DEC-8 (exceção ao passar de 366 dias) e DEC-9 (borda da própria faixa) decididas por Rafael Santos no chat; AC-6, AC-8, AC-11 e REQ-7 fechados. Nenhuma decisão em aberto; checklist de completude conferido. Aguardando aprovação. |
| 2026-10-23 | Rascunho | Revisão autorizada por Rafael Santos no chat, sem transição de status: DEC-7 recebeu o esclarecimento do alcance ("sem seed" vale para configuração padrão, não para migração de dados de negócio existentes), mantida a decisão original; REQ-2 reescrito para o mesmo alcance, sem mudar o comportamento desta spec nem os ACs; vínculos acrescentados (`backlog: BL-004`, fonte versionada `specs/_fontes/prompt-sla.md` com o SHA-256, referência ao `specs/BACKLOG.md`); nota do §9 sobre a Q-10 ajustada ao registro já feito com o backlog. Checklist de completude conferido de novo. Aguardando aprovação. |
| 2026-10-24 | Rascunho | Referência ao `SPEC-TICKEST.md` acrescentada no contexto (atende RF-50 e RF-51), a pedido de Rafael Santos no chat, seguindo a regra 0.2 daquele documento. Sem mudança de requisito, critério, decisão ou tarefa. |
