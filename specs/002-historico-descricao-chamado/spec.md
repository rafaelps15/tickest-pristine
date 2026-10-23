---
id: 002
titulo: Registrar no histórico a alteração da descrição do chamado
status: Verificada        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos  # responsável pelo produto e pelo desenvolvimento
aprovada-em: 2026-10-22
branch: feat/spec-driven-development
---

# 002 — Registrar no histórico a alteração da descrição do chamado

## 1. Contexto
- **Pedido original:** "Spec 002 do UpdateTicket: rodar a spec-new para a falta de histórico ao editar só a descrição" (Rafael Santos, 2026-10-22, no chat, ao autorizar as correções da auditoria do ambiente).
- **Problema observado:** `PUT tickets/{id}` grava a nova descrição (`UpdateTicketCommandHandler.cs:55`), mas só acrescenta histórico quando o status muda (`UpdateTicketCommandHandler.cs:66-76`). Uma edição só da descrição não deixa registro em `ticket_histories`, o que contraria a regra permanente do `CLAUDE.md` ("Histórico de chamados: todo command que altera um chamado, uma mensagem ou um anexo grava o registro com `TicketHistoryEntries` no mesmo `SaveChangesAsync`").
- **Referências:**
  - `SYSTEM_SPECIFICATION.md`: 5.4 (fluxo "Tratar chamado"), C-18 (histórico), 8 (E-17, E-18).
  - Código: `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs`, `src/TickestPristine.Domain/Tickets/TicketHistoryAction.cs`, `src/TickestPristine.Application/TicketHistories/TicketHistoryEntries.cs` (modelo: `StatusChanged`, `:42-54`), `src/TickestPristine.Application/TicketHistories/TicketHistoryDescriptions.cs`, `src/TickestPristine.Infrastructure/Tickets/TicketHistoryConfiguration.cs:15-19` (ação até 30 caracteres; `oldValue` e `newValue` até 500), `src/TickestPristine.Infrastructure/Tickets/TicketConfiguration.cs:18` (descrição até 500).
  - Testes: `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs`, `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs:139,905-975`.
  - Spec relacionada: `specs/001-remover-responsavel/spec.md` reserva a ação `Unassigned = 11`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Alterar a descrição de um chamado grava um registro `DescriptionChanged` no histórico, com a descrição anterior em `oldValue` e a nova em `newValue`, no mesmo `SaveChangesAsync`. | `CLAUDE.md` ("Histórico de chamados"); DEC-1 |
| REQ-2 | Quando status e descrição mudam no mesmo `PUT`, o histórico segue a DEC-2. | DEC-2 |
| REQ-3 | Um `PUT` com a mesma descrição atual segue a DEC-3. | DEC-3 |
| REQ-4 | O histórico mostra a alteração ao usuário em pt-BR, conforme a DEC-5. | DEC-5; `CLAUDE.md` ("Textos em pt-BR") |
| REQ-5 | O comportamento atual da mudança de status e das recusas (chamado inexistente, sem permissão, inativo, transição inválida) não muda. | `SYSTEM_SPECIFICATION.md` 5.4; testes existentes |

## 3. Critérios de aceitação
Os ACs refletem as decisões DEC-1 a DEC-5 (todas com a opção (a)). "A" e "B" são marcadores de duas descrições válidas e diferentes (de 10 a 500 caracteres, `TicketValidationRules.cs:10-12`), por exemplo "Impressora do 2º andar não liga" e "Impressora do 2º andar não liga depois da troca do toner".

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado ativo com a descrição A e o dono com `tickets:update-own` | o dono envia `PUT tickets/{id}` com a descrição B e o mesmo status | resposta `204`; o histórico ganha um registro `DescriptionChanged` com `oldValue` A, `newValue` B e o dono em `changedByUserId`; nenhum registro `StatusChanged` |
| AC-2 | REQ-2 | um chamado Aberto com a descrição A e um chamador com `tickets:manage` | o chamador envia `PUT tickets/{id}` com a descrição B e o status Em andamento | resposta `204`; o histórico ganha exatamente dois registros novos: `StatusChanged` (Aberto → Em andamento) e `DescriptionChanged` (`oldValue` A, `newValue` B) |
| AC-3 | REQ-3, REQ-5 | um chamado Aberto com a descrição A | um chamador com `tickets:manage` envia `PUT tickets/{id}` com a descrição A e o status Em andamento | resposta `204`; o histórico ganha só o registro `StatusChanged` |
| AC-4 | REQ-3 | um chamado ativo com a descrição A | o dono envia `PUT tickets/{id}` com a descrição A e o mesmo status | resposta `204`; nenhum registro novo no histórico |
| AC-5 | REQ-4 | um chamado cuja descrição foi alterada (AC-1) | alguém com acesso consulta `GET tickets/{id}/history` | o registro aparece com a ação `12` (`DescriptionChanged`) e a descrição "Descrição alterada" |
| AC-6 | REQ-5 | um chamado inativo, ou um chamador sem permissão | o chamador envia `PUT tickets/{id}` com a descrição B | as respostas atuais (`400 Tickets.NotActive`, `403`) se mantêm; nenhum registro novo no histórico |
| AC-7 | REQ-5 | um id de chamado inexistente, ou um chamado Aberto com a descrição A | um chamador com `tickets:manage` envia `PUT tickets/{id}` com a descrição B e, no segundo caso, o status Fechado | as respostas atuais se mantêm (`404 Tickets.NotFound`; `400 Tickets.InvalidStatusTransition`); nenhum registro novo no histórico |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como registrar a alteração da descrição? | (a) ação nova `DescriptionChanged`, com a descrição anterior em `oldValue` e a nova em `newValue` (cabem: a descrição tem até 500 caracteres, como as duas colunas); (b) ação nova sem valores (a auditoria já guarda os valores); (c) não gravar histórico e registrar no `CLAUDE.md` que a regra não vale para a descrição | (a) ação `DescriptionChanged` com os valores antigo e novo | Rafael Santos, 2026-10-22 (chat: "DEC-1: ação `DescriptionChanged`, contendo os valores antigo e novo") |
| DEC-2 | Status e descrição mudam no mesmo `PUT` | (a) dois registros, `StatusChanged` e `DescriptionChanged`; (b) um registro só | (a) dois registros | Rafael Santos, 2026-10-22 (chat: "dois registros quando status e descrição mudarem na mesma operação") |
| DEC-3 | `PUT` com a descrição igual à atual | (a) nenhum registro (comparação exata, ordinal); (b) registrar mesmo assim | (a) nenhum registro, comparação exata | Rafael Santos, 2026-10-22 (chat: "nenhum registro quando a descrição não mudar") |
| DEC-4 | Número da ação nova no enum | (a) `DescriptionChanged = 12`, deixando o 11 para o `Unassigned` da spec 001; (b) o próximo número livre na hora da implementação | (a) `DescriptionChanged = 12` | Rafael Santos, 2026-10-22 (chat: "número 12") |
| DEC-5 | Texto exibido no histórico | (a) "Descrição alterada"; (b) um texto com os valores antigo e novo | (a) "Descrição alterada" | Rafael Santos, 2026-10-22 (chat: "texto `Descrição alterada`") |

## 5. Fora do escopo
- O `TicketUpdatedDomainEvent` disparado mesmo quando nada muda (`UpdateTicketCommandHandler.cs:64`).
- Editar o título do chamado (o `PUT tickets/{id}` só recebe descrição e status).
- A descrição obrigatória mesmo quando só o status muda (`SYSTEM_SPECIFICATION.md` 5.4, item 10).

## 6. Plano técnico
- **Arquivos a alterar** (caminhos do `CLAUDE.md`, "Mapa do repositório"; referência viva: o registro `StatusChanged` no mesmo handler):
  - `src/TickestPristine.Domain/Tickets/TicketHistoryAction.cs` (`DescriptionChanged = 12`, DEC-4);
  - `src/TickestPristine.Application/TicketHistories/TicketHistoryEntries.cs` (fábrica `DescriptionChanged(ticketId, changedByUserId, oldDescription, newDescription, occurredAtUtc)`) e `TicketHistoryDescriptions.cs` ("Descrição alterada", DEC-5);
  - `src/TickestPristine.Application/Tickets/Update/UpdateTicketCommandHandler.cs` (guarda a descrição anterior antes de atribuir a nova; grava `DescriptionChanged` só quando `string.Equals(anterior, nova, StringComparison.Ordinal)` for falso, DEC-3; mantém o `StatusChanged` independente, DEC-2);
  - testes em `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs`, `tests/TickestPristine.Application.UnitTests/TicketHistories/TicketHistoryEntriesTests.cs` e `TicketHistoryDescriptionsTests.cs`, e em `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` ou `TicketHistories/TicketHistoriesTests.cs`.
- **Contratos:** a rota e o corpo de `PUT tickets/{id}` não mudam; o histórico ganha a ação `12` (`DescriptionChanged`) na resposta de `GET tickets/{id}/history`.
- **Testes por regra:** DEC-1 → AC-1 (unitário do handler confere ação, `oldValue`, `newValue` e `changedByUserId`; unitário da fábrica em `TicketHistoryEntriesTests`); DEC-2 → AC-2 (unitário e integração contam exatamente dois registros novos); DEC-3 → AC-3 e AC-4 (unitário confere ausência de `DescriptionChanged` com descrição igual); DEC-4 → AC-5 (integração confere `Action == 12`); DEC-5 → AC-5 (`InlineData(TicketHistoryAction.DescriptionChanged, "Descrição alterada")` em `TicketHistoryDescriptionsTests` e integração na leitura do histórico).
- **Mudança no banco:** não. A ação é gravada como texto de até 30 caracteres, e "DescriptionChanged" cabe.
- **Riscos:**
  - R-1: o `Down` da migration `20261022023422_Store_Ticket_Enums_As_Names` não conhece a ação nova (`CASE` sem `ELSE`, coluna obrigatória); a reversão dessa migration falharia por NOT NULL com registros `DescriptionChanged` gravados. Mesmo risco do R-1 da spec 001; só afeta reversão.
  - R-2: um cliente que trate o número da ação e não conheça o novo valor pode quebrar ao exibir o histórico; o front-end não está neste repositório.
  - R-3: o número 12 pressupõe o 11 reservado ao `Unassigned` da spec 001 (DEC-4); se esta spec for implementada antes da 001, o enum fica com um intervalo (11 livre) até a 001 ser implementada.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Acréscimo no Domain: ação de histórico `DescriptionChanged` | AC-1, AC-2, AC-5 | add-feature | [x] |
| T-2 | Fábrica `DescriptionChanged` em `TicketHistoryEntries` e texto em `TicketHistoryDescriptions` | AC-1, AC-5 | add-feature | [x] |
| T-3 | `UpdateTicketCommandHandler`: gravar o registro quando a descrição mudar, conforme as DECs 1 a 3 | AC-1, AC-2, AC-3, AC-4, AC-6, AC-7 | add-feature | [x] |
| T-4 | Testes unitários do handler, da fábrica e do texto; testes de integração por HTTP | AC-1 a AC-7 | add-tests | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_RecordDescriptionChanged_WhenOwnerChangesOnlyDescription`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_RecordDescriptionChanged_WhenOnlyDescriptionChanges` e `Handle_Should_RecordDescriptionChanged_WhenDescriptionDiffersOnlyInCase`; `TicketHistories/TicketHistoryEntriesTests.cs` › `DescriptionChanged_Should_RecordOldAndNewDescription` | HTTP: o dono troca só a descrição (A → B, status Aberto) → 204 e, além da abertura, um único registro `DescriptionChanged` com `oldValue` A, `newValue` B e o dono em `changedByUserId` (portanto nenhum `StatusChanged`); unitário: o mesmo pelo handler, e a diferença só de maiúsculas também grava (comparação ordinal, DEC-3); fábrica: ação, valores, autor e data | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_RecordStatusAndDescriptionChanges_WhenManagerChangesBoth`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_RecordStatusAndDescriptionChanges_WhenManagerChangesBoth` e `Handle_Should_UpdateTicket_WhenOwnerHasUpdateOwnPermission` | quem tem `tickets:manage` muda a descrição (A → B) e o status (Aberto → Em andamento) → 204 e exatamente dois registros novos: `StatusChanged` (`Open` → `InProgress`) e `DescriptionChanged` (A → B); o teste existente do dono passou a conferir as duas ações | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_RecordOnlyStatusChanged_WhenDescriptionIsUnchanged`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_RecordOnlyStatusChanged_WhenDescriptionIsUnchanged` | quem tem `tickets:manage` envia a mesma descrição A com o status Em andamento → 204 e um único registro novo, `StatusChanged` | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_NotRecordHistory_WhenNothingChanges`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_RecordNothing_WhenDescriptionAndStatusAreUnchanged` | o dono envia a mesma descrição e o mesmo status → 204 e nenhum registro além da abertura | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_ShowDescriptionChangeInHistory`; `TicketHistories/TicketHistoryDescriptionsTests.cs` › `For_Should_ReturnFixedText_WhenActionHasNoValues` (caso `DescriptionChanged`) | depois de trocar a descrição, `GET tickets/{id}/history` traz um item com `action` 12 e a descrição "Descrição alterada"; o texto também é conferido direto | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_NotRecordHistory_WhenTicketIsNotActive` e `Update_Should_NotRecordHistory_WhenCallerLacksPermission` | chamado Cancelado recebe B → 400 `Tickets.NotActive` e o número de registros não muda; quem não é dono nem tem `tickets:manage` → 403 e nenhum registro além da abertura | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |
| AC-7 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_ReturnNotFound_WhenManagerUpdatesMissingTicket` e `Update_Should_NotRecordHistory_WhenManagerSendsInvalidStatusTransition` | quem tem `tickets:manage`: id inexistente → 404 `Tickets.NotFound` e nenhum registro para o id; chamado Aberto com B e status Fechado → 400 `Tickets.InvalidStatusTransition` e nenhum registro além da abertura | Aprovado em 2026-10-23 17:53 (execução da `spec-verify`, relatório TRX por teste) |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx` em 2026-10-23 17:53: 0 avisos, 0 erros.
- **Testes:** `dotnet test TickestPristine.slnx --no-build --logger trx` em 2026-10-23 17:53: 742 aprovados, 0 com falha, 0 ignorados (ArchitectureTests 10/10; Application.UnitTests 462/462; IntegrationTests 270/270). Os 17 testes da rastreabilidade aparecem como `Passed` no TRX, inclusive o caso `DescriptionChanged` de `TicketHistoryDescriptionsTests`.
- **Revisão:** `ca-review` sobre os arquivos do plano técnico: nenhum achado bloqueante, nenhuma violação de convenção, nenhuma lacuna de teste. O handler continua usando `IDateTimeProvider` e grava o histórico no mesmo `SaveChangesAsync`; o texto ao usuário está em pt-BR. Veredito: pronto para commit.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`, em 2026-10-23: cabeçalho (estado documentado), 2.7 (C-18), 4.4 (enum 1–12), 5.4 (persistência e evidências), 6.1 (execução da verificação), 6.9 (specs existentes) e 8 (E-44). No mesmo passo, as afirmações que ficaram desatualizadas com a entrega da spec 001 (cabeçalho, 6.9 e E-40) passaram a citar o commit `b0e78b0`. Previsto na spec: 2.7 (C-18), 5.4 (persistência: `DescriptionChanged`), 8 (evidência nova).
- **CI:** não executado (a branch não foi enviada ao GitHub).
- **Entrega:** <commit ou PR, e a autorização>

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-22 | Rascunho | Spec criada pela `spec-new` a partir do pedido; 5 decisões em aberto (DEC-1 a DEC-5), com recomendação; os ACs assumem as recomendações. Aguardando decisões e aprovação. |
| 2026-10-22 | Rascunho | Ajustes da revisão independente: descrições dos ACs como marcadores de textos válidos (mínimo de 10 caracteres); AC-7 (chamado inexistente e transição inválida, já citados no REQ-5); REQ-1 compatível com a opção (c) da DEC-1; AC-3 ligado também ao REQ-3. Aguardando decisões e aprovação. |
| 2026-10-22 | Rascunho | DEC-1 a DEC-5 decididas por Rafael Santos no chat, todas com a opção (a). Na mensagem, a numeração das decisões diferia da spec (o chat chamou de DEC-2 o número 12, de DEC-3 os dois registros e de DEC-4 o "nenhum registro"); o registro segue o conteúdo de cada escolha, que é inequívoco. REQ-1, AC-2 e AC-5, plano e "Testes por regra" ajustados às decisões. Checklist de completude atendido. Aguardando aprovação explícita da spec. |
| 2026-10-22 | Aprovada | Aprovada explicitamente por Rafael Santos no chat ("Aprovo formalmente a Spec 002 e as decisões DEC-1 a DEC-5 anteriormente definidas. [...] Esta aprovação não autoriza commits nem a implementação automática."). Implementação só com `/spec-implement 002`. |
| 2026-10-23 | Em implementação | Início da implementação por `/spec-implement 002`, digitado por Rafael Santos no chat, sobre o commit `d0e18f5` (spec 001 entregue: a ação 11 já é `Unassigned`, então o R-3 não se aplica). Linha de base: a execução da `spec-verify` da spec 001 em 2026-10-23 16:55 (0 avisos; 726 testes aprovados), sem mudança de código depois dela. |
| 2026-10-23 | Em implementação | T-1 a T-4 concluídas, com as skills `add-feature` e `add-tests`, só nos arquivos do plano técnico; build e testes rodados ao fim de cada tarefa. Na T-3, o teste existente `UpdateTicketCommandHandlerTests.Handle_Should_UpdateTicket_WhenOwnerHasUpdateOwnPermission` falhou porque esperava um único registro e, pela DEC-2, agora há dois; a asserção foi ajustada à regra aprovada (arquivo do plano, T-4). Última execução: 0 avisos e 0 erros; 742 testes aprovados (10 de arquitetura, 462 unitários, 270 de integração), sem falhas. Rastreabilidade preenchida (coluna "Resultado" fica para a `spec-verify`). Aguardando verificação. |
| 2026-10-23 | Verificada | Verificação aprovada pela `spec-verify`: build com 0 avisos e 0 erros; 742 testes aprovados e 0 com falha na execução de 2026-10-23 17:53; os 7 ACs cobertos por testes lidos e conferidos contra Dado, Quando e Então; `ca-review` sem achados. Documentação de estado atualizada (ver "Evidências de verificação"). Ainda não entregue: commit e PR dependem de autorização explícita. |
