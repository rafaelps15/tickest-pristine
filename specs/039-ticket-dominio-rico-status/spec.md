---
id: 039
titulo: Ticket no domínio rico — parte 1 (entidade e transições de status)
status: Verificada              # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-29
branch: refactor/SPEC-039-ticket-rich-domain-status                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 039 — Ticket no domínio rico — parte 1 (entidade e transições de status)

## 1. Contexto
- **Pedido original:** Rafael Santos, 2026-10-29: "precisamos prosseguir na correção das demais partes do codigo!"; na escolha da próxima parte: "Ticket, em 2 a 3 specs" e "Descoberta já, branch depois".
- **Referências:** `CLAUDE.md` › "Em migração" (o `Ticket` é a próxima migração depois da spec 038); `src/TickestPristine.Domain/Tickets/Ticket.cs`; `src/TickestPristine.Application/Tickets/TicketStatusTransitions.cs`; handlers em `src/TickestPristine.Application/Tickets/`; spec 038 como modelo da migração.

## 2. Requisitos

| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | `Ticket` rica, no padrão da `add-entity` e da `domain-modeling`: um único construtor privado vazio, `private set` em todas as propriedades, sem guardas. | pedido; DEC-1 |
| REQ-2 | `Ticket.Create(string title, string description, TicketPriority priority, Sector sector, User requester, User? assignee, TicketSlaPlan slaPlan, DateTime dueAtUtc, DateTime slaWarningAtUtc, Guid openedByUserId, DateTime utcNow)` monta o chamado com inicializador de objeto (`Status = Open`, `CreatedAtUtc = LastActivityAtUtc = utcNow`) e dispara `TicketCreatedDomainEvent(Id, openedByUserId)`; `ticket.RecordOpening(Guid openedByUserId)` devolve a entrada `Created` do histórico. | DEC-4, DEC-6, DEC-8 |
| REQ-3 | As regras de estado de `TicketStatusTransitions` passam para o `Ticket`: `IsActive`, `CanTransitionTo(TicketStatus)` e `static IsActiveStatus(TicketStatus)` (para a política de cancelamento, as projeções e o seeder); `TicketStatusTransitions.cs` sai. | pedido; DEC-5 |
| REQ-4 | Métodos de comportamento que mudam o estado, disparam o próprio evento e devolvem a entrada do histórico (padrão 7 da `domain-modeling`): `Update(string description, TicketStatus status, Guid changedBy, DateTime utcNow)` → `IReadOnlyList<TicketHistory>` (status e descrição, nessa ordem, só o que mudou; `ResolvedAtUtc` na primeira resolução; sempre `TicketUpdatedDomainEvent`, e `TicketStatusChangedDomainEvent` quando o status muda); `Assign(User assignee, Guid changedBy, DateTime utcNow)` (`Assigned` ou `Reassigned`, `TicketReassignedDomainEvent`); `Unassign(Guid changedBy, DateTime utcNow)`; `Close(Guid changedBy, DateTime utcNow)`; `Reopen(Guid changedBy, DateTime utcNow)` (`Reopened`, `TicketReopenedDomainEvent`, sem mexer em `ResolvedAtUtc`, `DueAtUtc` e `SlaWarningAtUtc`); `Delete(Guid changedBy, DateTime utcNow)` (`DeletedAtUtc`, `Deleted`, `TicketDeletedDomainEvent`). | pedido; DEC-6, DEC-7, DEC-11 |
| REQ-5 | Os handlers de criar, atualizar, atribuir, remover responsável, fechar, reabrir e excluir carregam, conferem na ordem de hoje com as perguntas da entidade (`IsActive`, `CanTransitionTo`, `IsAssignedTo`, `AssignedToUserId is null`), chamam o método, adicionam as entradas do histórico e salvam uma vez; nenhum atribui propriedade do `Ticket`, chama `Raise` nele ou usa as fábricas de `TicketHistoryEntries` do chamado. A criação carrega o setor e os usuários (solicitante, responsável) numa consulta cada e os passa ao `Create`. | DEC-6, DEC-8 |
| REQ-6 | `TicketHistoryEntries` perde as fábricas que viraram métodos do `Ticket` (`Created`, `StatusChanged`, `Assigned`, `Reassigned`, `Unassigned`, `DescriptionChanged`); fica só o `Create` genérico, usado por mensagens e anexos até a parte 3. | DEC-4, DEC-7 |
| REQ-7 | `TicketActivityInterceptor` grava `LastActivityAtUtc` pela API do EF (`entry.Property(...).CurrentValue`), sem setter público. | DEC-9 |
| REQ-8 | `SampleDataSeeder` monta os chamados pelos métodos do `Ticket` (`Create` com o responsável quando o caminho passa por Em andamento, `Update` a cada passo, com a data de cada passo), com o mesmo histórico de hoje (`Created` e um `StatusChanged` por passo); o cronômetro de SLA continua montado como hoje (parte 2). | DEC-10 |
| REQ-9 | Nenhum comportamento observável muda: rotas, payloads, códigos HTTP, mensagens, ordem das conferências, eventos, histórico e cronômetros iguais. Exceção aceita: o seed de exemplo passa a disparar os eventos do chamado no `SaveChangesAsync` (não há handler para eles). | DEC-1, DEC-10 |
| REQ-10 | Testes: `TicketTests` novo com os métodos da entidade; os testes que montam `new Ticket { ... }` passam a usar `Ticket.Create` e os métodos (estados difíceis de alcançar, como `WaitingOn`, pela API do EF no arranjo); `Ticket` entra em `RichEntities`. | DEC-12 |
| REQ-11 | `CLAUDE.md`: `Ticket` e `TicketStatusTransitions` saem de "Em migração"; a linha de `TicketHistoryEntries` diz que só o `Create` genérico ficou; a nota de próxima migração aponta a parte 2 (SLA). `SYSTEM_SPECIFICATION.md` atualizado nas seções afetadas. | DEC-4 |

## 3. Critérios de aceitação

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | 2 | setor, solicitante, responsável e plano de SLA | `Ticket.Create(...)` e `RecordOpening(...)` | `Id` novo, `Status` Open, campos e datas informados, `LastActivityAtUtc = CreatedAtUtc`, `TicketCreatedDomainEvent` com o id e quem abriu; a entrada `Created` tem o id, quem abriu e a data |
| AC-2 | 3 | cada `TicketStatus` | `IsActive`, `IsActiveStatus` e `CanTransitionTo` | ativo só Open e InProgress; as transições permitidas são exatamente as de hoje, e o mesmo status não é transição |
| AC-3 | 4 | um chamado ativo | `Update` com descrição e status novos, só um deles, ou nenhum | só o que mudou vira entrada (status antes da descrição); `ResolvedAtUtc` só na primeira resolução; `TicketUpdatedDomainEvent` sempre; `TicketStatusChangedDomainEvent` só quando o status muda |
| AC-4 | 4 | um chamado com e sem responsável | `Assign` e `Unassign` | `AssignedToUserId` muda; entrada `Assigned`, `Reassigned` ou `Unassigned` com os valores de hoje; `TicketReassignedDomainEvent` e `TicketUnassignedDomainEvent` com o anterior e o novo |
| AC-5 | 4 | um chamado resolvido, fechado ou cancelado | `Close`, `Reopen` e `Delete` | status, `DeletedAtUtc`, entradas `StatusChanged`/`Reopened`/`Deleted` e eventos como hoje; `Reopen` não mexe em `ResolvedAtUtc`, `DueAtUtc` e `SlaWarningAtUtc` |
| AC-6 | 5, 9 | os testes de handler e de integração existentes de chamados | `dotnet test` | todos passam com as asserções de antes (só o arranjo muda): erros, ordem das conferências, histórico, eventos e cronômetros |
| AC-7 | 5, 6 | o código da Application | busca por atribuição a propriedade do `Ticket`, `Raise(new Ticket…` fora do Domain, `TicketStatusTransitions` e as fábricas removidas de `TicketHistoryEntries` | nenhuma ocorrência; a revisão não aponta bloqueante |
| AC-8 | 7 | um comando que grava histórico num chamado carregado e em outro não carregado | `SaveChangesAsync` | `LastActivityAtUtc` igual ao de hoje nos dois casos (testes de atividade existentes) |
| AC-9 | 8 | `SampleData` ligado | o seed roda (teste de integração do seeder ou execução da API) | os chamados de exemplo têm os mesmos status, datas, responsáveis e histórico de hoje |
| AC-10 | 10 | `tests/TickestPristine.ArchitectureTests` | `dotnet test` | `RichEntities_Should_NotHaveNonPrivateSetters` passa com `Ticket` na lista |
| AC-11 | 9 | o modelo do EF | `dotnet ef migrations has-pending-model-changes` | nenhuma mudança de modelo; nenhuma migration nova |
| AC-12 | 9, 10 | build e suíte inteira | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; os testes da linha de base continuam aprovados, mais os novos |
| AC-13 | 11 | `CLAUDE.md` e `SYSTEM_SPECIFICATION.md` | `python -m unittest discover -s scripts -p "test_*.py"` e leitura | os testes de instruções passam; "Em migração" sem `Ticket` e `TicketStatusTransitions` |

## 4. Decisões

| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Divisão da migração do `Ticket` | uma spec / 2 a 3 specs | 2 a 3 specs; esta é a parte 1. Rafael Santos no chat: "Ticket, em 2 a 3 specs" | Rafael Santos, 2026-10-29 |
| DEC-2 | Quando criar a branch | já, a partir da 038 / depois do merge da 038 | Descoberta já; branch a partir da `origin/main` depois do merge da 038. Rafael Santos no chat: "Descoberta já, branch depois" | Rafael Santos, 2026-10-29 |
| DEC-3 | Testes por tarefa | suíte inteira / unitários e arquitetura por tarefa, integração no fim | Unitários e arquitetura por tarefa; a suíte de integração roda no fim da spec e na verificação. Rafael Santos no chat: "Sim, integração no fim" | Rafael Santos, 2026-10-29 |
| DEC-4 | O que entra em cada parte | por arquivo / por fronteira de regra | Parte 1 (esta): `Ticket`, transições de status e as entradas do histórico que o próprio chamado grava. Parte 2: SLA (`TicketSlaTimer`, `TicketSlaClock`, `TicketSla`, monitoramento). Parte 3: mensagens, anexos, `TicketHistory` rica, `TicketMessageEntries` e os guards de autorização. Decidido pelo agente com a delegação de Rafael Santos no chat: "You have full authority to make all technical decisions necessary to complete the task" | agente, 2026-10-29 |
| DEC-5 | Onde fica a regra de "ativo" usada fora de uma entidade carregada (projeções, política de cancelamento, seeder) | duplicar o predicado / `static IsActiveStatus` no `Ticket` | `Ticket.IsActiveStatus(TicketStatus)`, usado pelo `IsActive` e pelos chamadores de hoje; as cópias em SQL e LINQ (`WhereActive`, `ActiveStatuses`, SQL do `TicketWaitingInterceptor`) ficam como estão, porque o EF não traduz o método. Mesma delegação | agente, 2026-10-29 |
| DEC-6 | Conferências de estado | o método devolve `Result` e confere / o handler confere com as perguntas da entidade e o método não repete | O handler confere com `IsActive`, `CanTransitionTo` e `IsAssignedTo`, porque a ordem de hoje intercala essas conferências com as de permissão e de consulta (padrão 8: "quando o handler já conferiu o estado, o método não repete a conferência"); a regra continua só na entidade. Mesma delegação | agente, 2026-10-29 |
| DEC-7 | Quem cria a entrada do histórico | `TicketHistoryEntries` / o método do `Ticket` devolve a entrada e o handler a adiciona | O método do `Ticket` (padrão 7 da `domain-modeling` e desvio "Histórico de chamados" do `CLAUDE.md`); a abertura usa `RecordOpening`, porque o `Create` já devolve o chamado. `TicketHistory` continua anêmica até a parte 3, porque mensagens e anexos ainda a criam pela Application. Mesma delegação | agente, 2026-10-29 |
| DEC-8 | O que o `Create` recebe | ids / as entidades referenciadas | `Sector` e `User` (solicitante e responsável), carregados pelo handler (regra da `add-feature`: o handler carrega a entidade referenciada e a passa ao `Create`); o usuário logado entra na mesma consulta sem a exigência de estar ativo, como hoje. Mesma delegação | agente, 2026-10-29 |
| DEC-9 | `LastActivityAtUtc` gravado pelo interceptor | setter público / API do EF | API do EF no interceptor (`entry.Property(t => t.LastActivityAtUtc).CurrentValue`); o `private set` vale para todo o código de aplicação. Mesma delegação | agente, 2026-10-29 |
| DEC-10 | Seeder de exemplo | manter os setters / usar os métodos | Usar os métodos, com o mesmo resultado de hoje (o responsável entra pelo `Create`, os passos pelo `Update`); o cronômetro de SLA do seed fica para a parte 2. Mesma delegação | agente, 2026-10-29 |
| DEC-11 | `Update` sem mudança | `bool` (evento só quando muda) / evento sempre | Evento `TicketUpdatedDomainEvent` sempre, como hoje (mesmo desvio do `Update` de departamento e setor); só o histórico e o `TicketStatusChangedDomainEvent` dependem de mudança. Mesma delegação | agente, 2026-10-29 |
| DEC-12 | Arranjo dos testes que precisam de estados específicos | setters / métodos e API do EF | Métodos do `Ticket`; `WaitingOn` e datas no passado que o domínio não produz, pela API do EF no arranjo (como na spec 038). Mesma delegação | agente, 2026-10-29 |
| DEC-13 | `WaitingOn`, que só o banco grava | `private set` / só `get` | Só `get`: o analisador do build recusa um `private set` nunca usado (S1144), e o EF grava pela propriedade mapeada na configuração; o `TestDbContext` mapeia a propriedade como a configuração real. Decidido pelo agente com a mesma delegação | agente, 2026-10-29 |
| DEC-14 | `<summary>` existentes removidos na implementação (cerca de 55: testes, propriedades `LastActivityAtUtc` e `WaitingOn`, `SampleUserGroups` e `StatusPaths` do seeder, `TicketHistoryEntries.Create` e as fábricas excluídas) | restaurar / manter removidos | Manter removidos, inclusive o de `TicketHistoryEntries.Create`: a regra de comentários não admite `<summary>` em testes e propriedades, e as fábricas foram excluídas. Exceção desta spec à regra "nenhum `<summary>` existente é removido"; o `CLAUDE.md` não muda. Rafael Santos no chat: "Não vai restaurar nada !" e "Prossiga para o próximo passo" | Rafael Santos, 2026-10-30 |

## 5. Fora do escopo

- SLA: `TicketSlaTimer`, `TicketSlaClock`, `TicketSla` e o monitoramento (parte 2).
- Mensagens, anexos, `TicketHistory` rica, `TicketMessageEntries`, `TicketAccessGuard`, `TicketAssigneeEligibility` e os demais guards (parte 3).
- `TicketCancellationPolicy`: continua na Application (não está em "Em migração"); só troca a dependência para `Ticket.IsActiveStatus`.
- As cópias do predicado de "ativo" em SQL e LINQ (DEC-5).
- Qualquer mudança de contrato, de banco ou de comportamento.

## 6. Plano técnico

- **Domain:** `Ticket.cs` rico (REQ-1 a REQ-4); `<summary>` só nos métodos não óbvios (`Update`, `RecordOpening`, `Reopen`).
- **Application:** os 7 handlers de chamado (REQ-5); `TicketHistoryEntries` reduzida (REQ-6); `TicketStatusTransitions.cs` excluído; `TicketCancellationPolicy` com `Ticket.IsActiveStatus`.
- **Infrastructure:** `TicketActivityInterceptor` (REQ-7); `SampleDataSeeder` (REQ-8).
- **Testes:** `Tickets/TicketTests.cs` novo; arranjo dos ~30 arquivos que montam `Ticket`; `RichEntities`.
- **Contratos:** nenhum muda. **Banco:** sem migration.
- **Documentação:** `CLAUDE.md` (REQ-11) e `SYSTEM_SPECIFICATION.md`.
- **Riscos:**

| Risco | Mitigação |
|---|---|
| Mudar a ordem das conferências ao mover a regra | DEC-6: o handler mantém a ordem; AC-6 com as asserções de antes |
| O EF mapear `IsActive` como coluna | Propriedade só de leitura, sem campo; AC-11 confere que o modelo não mudou |
| `LastActivityAtUtc` divergir com o `Create` gravando a data | A data do `Create` é a mesma da entrada `Created`; AC-8 |
| Arranjo de ~30 arquivos de teste | Mudança mecânica, só no arranjo; asserções intocadas |
| Seed de exemplo diferente | AC-9 compara status, datas, responsáveis e histórico |

## 7. Tarefas

| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Linha de base: build, testes e `has-pending-model-changes` | AC-11, AC-12 | manual | [x] |
| T-2 | `Ticket` rica, transições e `TicketTests` | AC-1 a AC-5 | domain-modeling, add-tests | [x] |
| T-3 | Os 7 handlers, `TicketHistoryEntries`, `TicketCancellationPolicy`; sai `TicketStatusTransitions` | AC-6, AC-7 | add-feature | [x] |
| T-4 | `TicketActivityInterceptor` e `SampleDataSeeder` | AC-8, AC-9 | add-entity | [x] |
| T-5 | Arranjo dos testes existentes e `RichEntities` | AC-6, AC-10, AC-12 | add-tests | [x] |
| T-6 | `CLAUDE.md` e `SYSTEM_SPECIFICATION.md` | AC-13 | manual | [x] |
| T-7 | Verificação | todos | spec-verify | [x] |

## 8. Rastreabilidade

| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 a AC-5 | `tests/TickestPristine.Application.UnitTests/Tickets/TicketTests.cs` › `Create_Should_OpenTicketAndRaiseEvent_WhenCalled`, `Create_Should_LeaveTicketUnassigned_WhenAssigneeIsNull`, `RecordOpening_Should_RecordOpenStatusByCreator_WhenCalled`, `IsActive_Should_*`, `IsActiveStatus_Should_*`, `CanTransitionTo_Should_AllowOnlyPermittedTransitions_WhenTargetStatusIsGiven` (25 casos), `IsAssignedTo_Should_*`, os 6 `Update_Should_*`, os 2 `Assign_Should_*`, `Unassign_Should_*`, `Close_Should_*`, `Reopen_Should_OpenAndRecordReopened_WhenTicketIsInactive`, `Delete_Should_MarkDeletedAndRecordDeleted_WhenCalled` | Conferem campos, datas, eventos (a ordem exata `Updated`→`StatusChanged`), as entradas do histórico (ação, valores, autor, data) e a tabela inteira de transições; `Reopen` confere que `ResolvedAtUtc`, `DueAtUtc` e `SlaWarningAtUtc` não mudam | Aprovado em 2026-10-30 |
| AC-6 | testes existentes de `Tickets/`, `TicketMessages/`, `TicketAttachments/`, `TicketHistories/`, `Authorization/` e integração (`TicketsTests`, `TicketActivityTests`, `TicketConcurrencyTests`, `SlaMonitorBackgroundServiceTests`) | Só o arranjo mudou (pelo `TicketTestData`, com `Create` e os métodos; API do EF só para `WaitingOn`, `LastActivityAtUtc` e um status fora do enum); as asserções são as de antes; os 5 testes de `TicketHistoryEntriesTests` passaram para `TicketTests` | Aprovado em 2026-10-30 |
| AC-7 | busca no código e revisão independente | Nenhuma atribuição a propriedade do `Ticket`, `Raise` nele fora do Domain, `TicketStatusTransitions` ou fábrica removida de `TicketHistoryEntries` | Aprovado em 2026-10-30 |
| AC-8 | `tests/TickestPristine.IntegrationTests/Tickets/TicketActivityTests.cs` (todos) | Conferem `LastActivityAtUtc` com o chamado carregado (gravação pela API do EF) e não carregado (`UPDATE` depois do commit) | Aprovado em 2026-10-30 |
| AC-9 | Comparação do seed (verificação descrita) | O `SampleDataSeeder` da `origin/main` e o novo rodaram com o mesmo relógio fixo num banco sem chamados; o levantamento de chamados, histórico e cronômetros (300 linhas, com e-mails no lugar dos ids) saiu idêntico | Aprovado em 2026-10-30 |
| AC-10 | `tests/TickestPristine.ArchitectureTests/Conventions/RichEntityTests.cs` › `RichEntities_Should_NotHaveNonPrivateSetters` | A lista tem `typeof(Ticket)` | Aprovado em 2026-10-30 |
| AC-11 | `tests/TickestPristine.IntegrationTests/Database/MigrationsTests.cs` › `Model_Should_HaveNoPendingChanges` (o portão SDD bloqueia o `dotnet ef` sobre `src/`; na T-1, `dotnet ef migrations has-pending-model-changes` sem mudança) | Confere `Database.HasPendingModelChanges()` falso: o modelo do EF não mudou e nenhuma migration nova foi criada | Aprovado em 2026-10-30 |
| AC-12 | `dotnet build` e `dotnet test` | 0 avisos; 1.339 aprovados (56 + 810 + 473) | Aprovado em 2026-10-30 |
| AC-13 | `scripts/test_instructions.py` e leitura | 14 testes OK; "Em migração" sem `Ticket` e `TicketStatusTransitions`; `SYSTEM_SPECIFICATION.md` atualizado | Aprovado em 2026-10-30 |

## 9. Evidências de verificação
- **Build (2026-10-29):** `dotnet build TickestPristine.slnx --no-incremental` com 0 avisos e 0 erros.
- **Testes (2026-10-29, trx):** suíte inteira 1.339 aprovados e 0 falhas (56 de arquitetura, 810 unitários, 473 de integração); depois das correções da revisão (comentários, nome de variável e um teste), unitários e arquitetura de novo: 810 e 56. `has-pending-model-changes` sem mudança. Seed comparado (AC-9) idêntico. Auditoria SDD 100%; `test_instructions.py` OK.
- **Revisão:** `ca-review` independente aprovou, sem bloqueantes. Corrigidos: pré-condição no `<summary>` de `Close`, `Reopen` e `Delete`; citação de spec retirada de um `<summary>`; `requesterId` no handler de criação; `<summary>` do `TicketHistoryEntries.Create` restaurado; ordem dos eventos conferida no teste do `Update`. Mantidos, com motivo: `WaitingOn { get; }` (DEC-13); `Guid.Empty` nos cronômetros da criação (até a parte 2).
- **Documentação atualizada:** `CLAUDE.md` (Em migração, desvio do `Update`, histórico de chamados, convenções) e `SYSTEM_SPECIFICATION.md` (seções 1, 2.5, 3.1, 3.2, 5.4, 5.10, 8 e o diagrama de camadas).
- **Verificação de 2026-10-30 (T-7):** `dotnet build TickestPristine.slnx --no-incremental` com 0 avisos e 0 erros; `dotnet test` (trx) com 1.339 aprovados e 0 falhas (56 + 810 + 473), entre eles os 53 de `TicketTests`, os 5 de `TicketActivityTests`, `RichEntities_Should_NotHaveNonPrivateSetters` e `MigrationsTests.Model_Should_HaveNoPendingChanges` (evidência do AC-11, porque o portão SDD bloqueia o `dotnet ef` sobre `src/`); auditoria SDD 100%, código de saída 0; `test_instructions.py` com 14 testes OK; busca do AC-7 sem ocorrências em `.cs`. Revisão `ca-review` independente: **reprova**, com um bloqueante: cerca de 55 `<summary>` existentes removidos (convertidos em `//` ou apagados), contra a regra permanente "nenhum `<summary>` existente é removido"; com a DEC-14, de Rafael Santos, os `<summary>` ficam removidos e o achado deixa de bloquear. Veredito final: aprova. Estão em `TicketHistoryEntries.Create` (o "restaurado" acima não está na árvore), em `Ticket.LastActivityAtUtc` e `Ticket.WaitingOn`, em `SampleDataSeeder` (`SampleUserGroups`, `StatusPaths`) e em 14 arquivos de teste (`BaseHandlerTest` 10, `TicketsTests` 22, `SlaMonitorBackgroundServiceTests` 4, entre outros); onde o texto citava uma spec, sai só a citação. Não bloqueantes: o handler de criação passa a devolver `UserErrors.NotFound` quando o usuário logado não existe no banco (antes, falha de FK); `slaPlan!` nos `TicketTestData`; `resolvedAtUtc` ignorado fora de Resolvido/Fechado no `TicketTestData`; arquivos em LF.
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-29 | Rascunho | Spec criada a partir do pedido de Rafael Santos. |
| 2026-10-29 | Em descoberta | Descoberta iniciada: mapa das escritas no `Ticket`, das transições de status e das classes estáticas ligadas. |
| 2026-10-29 | Em descoberta | Descoberta concluída pelo agente com a delegação de Rafael Santos ("You have full authority to make all technical decisions"): mapa das escritas no `Ticket` (9 handlers, 2 interceptors, seeder, ~30 arquivos de teste), DEC-4 a DEC-12 e spec escrita. Aguarda aprovação. |
| 2026-10-29 | Aprovada | Aprovada por Rafael Santos no chat: "Pode fazer 1,2 e 3" (merge do PR #54, aprovação da spec e início da implementação). |
| 2026-10-29 | Em implementação | `/spec-implement 039` por Rafael Santos, na branch `refactor/SPEC-039-ticket-rich-domain-status` criada da `origin/main` (`a72e07b`). Modo: tarefas em sequência sem parada entre elas e sem commits (pedido de Rafael Santos: "Do not commit under any circumstances, even after successful completion"). |
| 2026-10-29 | Em implementação | T-1 a T-6 feitas sem commits, como pedido. Linha de base: `src/` e `tests/` da `origin/main` iguais aos do `d4c841c`, verificado na spec 038. Arranjo dos testes convertido para o domínio; `TicketHistoryEntriesTests.cs` excluído (os 5 testes cobriam só as fábricas removidas e passaram para `TicketTests`); `TicketSlaClock` incluído em "Em migração" do `CLAUDE.md` (classe estática de regra, parte 2), ampliando o REQ-11; DEC-13 registrada. Falta a T-7 (verificação). |
| 2026-10-30 | Em implementação | Verificação (T-7) reprovada: build, testes, auditoria e ACs atendidos, mas a revisão achou um bloqueante, os `<summary>` existentes removidos (seção 9). Falta restaurar esses `<summary>` (sem citação de spec) e verificar de novo; o `SYSTEM_SPECIFICATION.md` fica para a próxima verificação. |
| 2026-10-30 | Verificada | Rafael Santos decidiu não restaurar os `<summary>` (DEC-14), e o bloqueante deixou de valer. Build, testes (1.339 aprovados) e auditoria (100%) da execução de 2026-10-30, todos os ACs cobertos e nenhum bloqueante restante. O `SYSTEM_SPECIFICATION.md` já estava atualizado desde a T-6 (seções 1, 2.5, 3.1, 3.2, 5.4, 5.10 e 8), e a verificação não muda nada nele. |
