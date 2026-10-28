---
id: 016
titulo: Cancelamento em 15 minutos pelo solicitante, exclusão e cancelamento por permissão e reabertura com motivo
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-016-cancel-window-delete-reopen-reason                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr: "#33"                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 016 — Cancelamento em 15 minutos pelo solicitante, exclusão e cancelamento por permissão e reabertura com motivo

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end (`tickest-pristine-front`), em 2026-10-27, no critique da tela "Meus Chamados": "Quem abriu o ticket deve poder cancelar seu ticket no máximo dentro de alguns minutos. Após isso ele deve perder esse direito. Quem poderá cancelar será somente o admin e as roles que tiverem essa permissão". Na mesma conversa escolheu a janela de 15 minutos, tirar o "Excluir" do solicitante e reabrir com motivo opcional que vira mensagem no chamado. Em seguida pediu esta spec ("3"), separada da 015 (DEC-4 da 015).
- **Referências:**
  - Spec 015 (listagem de "Meus Chamados"): altera o mesmo `OwnTicketResponse` e o mesmo slice `GetOwn` (DEC-8).
  - `SYSTEM_SPECIFICATION.md` 2.6: RN-06 (editar, excluir ou reabrir exige ser o dono com a permissão `*-own` ou ter `tickets:manage`), RN-22 (padrão de 403 com erro próprio), RN-26 (mensagens e cronômetros); seção do fluxo de status (linha 639); 4.3 (rotas).
- **Fatos do repositório:**
  - Cancelar é uma troca de status em `PUT tickets/{id}` (`Tickets/Update/UpdateTicketCommandHandler.cs`). O handler libera o dono com `tickets:update-own` ou quem tem `tickets:manage` (`TicketAccessGuard.EnsureOwnerOrManagerAsync`) e aceita qualquer transição válida de `TicketStatusTransitions` (`Open` → `InProgress`/`Canceled`; `InProgress` → `Resolved`/`Canceled`), sem limite de tempo. Ou seja, hoje o dono também pode mover o próprio chamado para Em Andamento e Resolvido.
  - Excluir (`Tickets/Delete/DeleteTicketCommandHandler.cs`) libera o dono com `tickets:delete-own` ou quem tem `tickets:manage`; a exclusão é lógica.
  - Reabrir (`Tickets/Reopen/ReopenTicketCommand.cs`) recebe só o id; libera o dono com `tickets:reopen-own` ou quem tem `tickets:manage`.
  - Permissões padrão (`Infrastructure/Roles/RolePermissionConfiguration.cs:33-50`): o Administrador recebe todas as do catálogo; o Atendente tem `tickets:manage` e `tickets:delete-own`, entre outras; o Colaborador tem `tickets:update-own`, `tickets:delete-own` e `tickets:reopen-own`. Uma migration que acrescenta vínculo precisa usar `INSERT ... ON CONFLICT DO NOTHING` (comentário em `RolePermissionConfiguration.cs:29-31`).
  - Mensagens têm até 4000 caracteres e gravam histórico `MessageAdded` (`TicketMessages/Post/*`).
  - Recusas de permissão em chamado usam 403 com erro de domínio próprio, como `Tickets.CannotAssignToOthers` (RN-22).
  - Limites de regra de negócio ficam em constantes do código, como `TicketAttachmentLimits`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | O dono do chamado, com `tickets:update-own`, pode cancelá-lo só até 15 minutos depois da abertura (`createdAtUtc` + 15 min). Depois disso, a tentativa é recusada com 403 `Tickets.CancelWindowExpired` e o chamado não muda. A janela conta só a partir da abertura; reabrir não a reinicia. | pedido; DEC-1; DEC-5; DEC-7 |
| REQ-2 | Fora da janela do dono, só cancela quem tem a permissão nova `tickets:cancel`, a qualquer momento e em qualquer chamado ativo. Por padrão, só o Administrador a tem; quem tem só `tickets:manage` deixa de poder cancelar e recebe 403 `Tickets.CancelForbidden`. | pedido; DEC-1 |
| REQ-3 | O dono, com `tickets:update-own`, continua podendo editar a descrição do chamado ativo, mas não muda mais o status para Em Andamento nem para Resolvido: essas trocas exigem `tickets:manage` (403 `Tickets.StatusChangeForbidden`). | DEC-4 |
| REQ-4 | Excluir um chamado exige a permissão nova `tickets:delete`, que por padrão só o Administrador tem. O dono não exclui mais pelo fato de ser dono, e `tickets:manage` sozinho também não basta (403 nos dois casos). A exclusão continua lógica. | pedido; DEC-2 |
| REQ-5 | A permissão `tickets:delete-own` sai do catálogo; a migration apaga a permissão e todos os vínculos dela com roles. | DEC-6 |
| REQ-6 | `POST tickets/{id}/reopen` aceita um motivo opcional de até 500 caracteres. Com motivo preenchido (depois do `Trim`), a reabertura grava também uma mensagem de quem reabriu, com esse texto, no mesmo `SaveChangesAsync`: as duas coisas acontecem juntas ou nenhuma. Sem motivo, ou só com espaços, reabre como hoje. Acima de 500 caracteres, 400 e nada muda. | pedido; DEC-3 |
| REQ-7 | O detalhe do chamado (`GET tickets/{id}`) e cada item de `GET tickets/mine` trazem `canCancel`, calculado para quem chama (chamado ativo e, além disso, dono dentro da janela ou com `tickets:cancel`), e `ownerCancelDeadlineUtc` (abertura + 15 min). | DEC-8 da 015; DEC-9 |
| REQ-8 | A janela de 15 minutos é uma constante de regra de negócio no código, num único lugar, usada pela regra e pelos campos de REQ-7. | DEC-10 |

## 3. Critérios de aceitação
Dados comuns: Maria (Colaboradora, dona dos chamados); Ana (Atendente, com `tickets:manage`); o administrador do seed; o relógio controlado pelo `IDateTimeProvider` dos testes.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | chamado de Maria aberto às 9h00 | Maria cancela às 9h14 | 204; status Cancelado; histórico `StatusChanged` |
| AC-2 | REQ-1 | chamado de Maria aberto às 9h00 | Maria cancela às 9h16 | 403 `Tickets.CancelWindowExpired`; status continua o de antes; nenhum histórico novo |
| AC-3 | REQ-1 | chamado de Maria aberto às 9h00, cancelado às 9h05 e reaberto às 9h10 | Maria cancela às 9h20 | 403 `Tickets.CancelWindowExpired` (a janela não recomeçou na reabertura) |
| AC-4 | REQ-2 | chamado de Maria aberto há 2 horas | Ana cancela; o administrador cancela | Ana: 403 `Tickets.CancelForbidden`, sem mudança; administrador: 204, status Cancelado |
| AC-5 | REQ-2 | uma role com `tickets:cancel` concedida pelo administrador | um usuário dessa role cancela um chamado de outra pessoa, aberto há 2 horas | 204 |
| AC-6 | REQ-3 | chamado ativo de Maria | Maria muda o status para Em Andamento; depois edita só a descrição | 403 `Tickets.StatusChangeForbidden` sem mudança; a edição da descrição dá 204 |
| AC-7 | REQ-4 | chamado de Maria | Maria exclui; Ana exclui; o administrador exclui | Maria e Ana: 403, chamado intacto; administrador: 204, exclusão lógica |
| AC-8 | REQ-5 | banco antes da migration, com `tickets:delete-own` vinculada a Atendente, Colaborador, Administrador e a uma role criada à mão | aplica a migration | a permissão não existe mais em `permissions` nem em `role_permissions`; `tickets:cancel` e `tickets:delete` existem e estão vinculadas só ao Administrador |
| AC-9 | REQ-6 | chamado Resolvido de Maria | Maria reabre com o motivo "  A impressora parou de novo.  " | 204; status Aberto; uma mensagem nova de Maria com "A impressora parou de novo."; histórico `Reopened` e `MessageAdded` |
| AC-10 | REQ-6 | chamado Resolvido de Maria | Maria reabre sem corpo, com motivo "   " e com motivo de 501 caracteres | sem corpo e com espaços: 204 sem mensagem nova; 501 caracteres: 400, chamado continua Resolvido e sem mensagem |
| AC-11 | REQ-6 | chamado Resolvido de Maria | Maria reabre com motivo (teste unitário do handler) | a mudança de status, a mensagem e os dois históricos são gravados numa única chamada de `SaveChangesAsync`; se essa chamada falhar, o handler não grava nada em separado depois |
| AC-12 | REQ-7 | chamado de Maria aberto às 9h00 | Maria consulta o detalhe e `GET tickets/mine` às 9h10 e às 9h20; o administrador consulta o detalhe às 9h20 | Maria às 9h10: `canCancel` = `true`; às 9h20: `false`; administrador às 9h20: `true`; nas três, `ownerCancelDeadlineUtc` = 9h15 |
| AC-13 | REQ-7 | chamado de Maria Cancelado | Maria e o administrador consultam o detalhe | `canCancel` = `false` para os dois (chamado inativo) |
| AC-14 | REQ-8 | o código | busca pela constante da janela (verificação descrita) | o valor de 15 minutos aparece num único lugar, usado pelo handler e pelas duas consultas |
| AC-15 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Passada a janela, quem cancela? | (a) permissão nova `tickets:cancel`, por padrão só o Administrador; (b) a mesma permissão, padrão Administrador e Atendente; (c) reusar `tickets:manage` | (a): segue "só o admin e as roles que tiverem essa permissão"; o Atendente deixa de cancelar até receber a permissão | Rafael Santos, 2026-10-27, no chat |
| DEC-2 | Quem exclui? | (a) permissão nova `tickets:delete`, só Administrador por padrão, e `tickets:delete-own` sai do Atendente e do Colaborador; (b) quem tem `tickets:manage` | (a) | Rafael Santos, 2026-10-27, no chat |
| DEC-3 | Reabrir com motivo no back-end? | (a) motivo opcional no corpo do reopen, gravado como mensagem no mesmo `SaveChanges`; (b) deixar como está (o front publica a mensagem depois) | (a): atômico; o front passa a fazer uma chamada só | Rafael Santos, 2026-10-27, no chat |
| DEC-4 | Restringir as outras trocas de status do dono? | (a) sim: o dono só edita a descrição e cancela na janela; (b) fora do escopo | (a) | Rafael Santos, 2026-10-27, no chat |
| DEC-5 | A janela recomeça na reabertura? | (a) não, conta só da abertura; (b) sim | (a): a janela existe para desfazer um chamado aberto por engano | Rafael Santos, 2026-10-27, no chat |
| DEC-6 | O que fazer com `tickets:delete-own` | (a) remover do catálogo, com a permissão e os vínculos; (b) manter sem uso | (a): a tela de Funções não mostra permissão que não faz nada | Rafael Santos, 2026-10-27, no chat |
| DEC-7 | Erro da janela vencida | (a) 403 com erro próprio, como RN-22; (b) 400 | (a): é recusa de permissão, no padrão do sistema | fato do repositório (RN-22), informado no chat em 2026-10-27 |
| DEC-8 | Base da branch | (a) depois do merge da 015; (b) independente, da `main` | (a): as duas mexem no `OwnTicketResponse` e no `GetOwn`; a 016 pode ser aprovada antes | Rafael Santos, 2026-10-27, no chat |
| DEC-9 | Como o front sabe se mostra "Cancelar" | (a) `canCancel` calculado para quem chama e `ownerCancelDeadlineUtc`; (b) só o prazo | (a): o front não recalcula a regra | Rafael Santos, 2026-10-27, no chat |
| DEC-10 | Janela fixa ou configurável | (a) constante no código, como `TicketAttachmentLimits`; (b) `appsettings` | (a) | Rafael Santos, 2026-10-27, no chat |

## 5. Fora do escopo
- A listagem de "Meus Chamados" (spec 015).
- Uma tela do front para o Administrador excluir chamados e o ajuste do front às regras novas (repositório `tickest-pristine-front`).
- Mudar quem edita a descrição, quem atribui ou quem fecha chamados.
- Motivo obrigatório na reabertura, ou motivo ao cancelar.

## 6. Plano técnico
- **Pré-requisito:** PR da spec 015 mergeado na `main` (DEC-8).
- **Arquivos a criar ou alterar:**
  - Domain:
    - `Tickets/TicketErrors.cs`: `CancelWindowExpired`, `CancelForbidden` e `StatusChangeForbidden`, os três como recusa de permissão (403).
    - `Tickets/TicketCancellationPolicy.cs` (novo, ou no lugar que o padrão de domínio rico da spec 012 indicar se ela já estiver na `main`): a constante de 15 minutos, o cálculo de `ownerCancelDeadlineUtc` e a regra "o dono ainda pode cancelar".
  - Application:
    - `Permissions/PermissionCodes.cs` e `PermissionCatalog.cs`: `tickets:cancel` e `tickets:delete` entram, `tickets:delete-own` sai.
    - `Tickets/Update/UpdateTicketCommandHandler.cs`: troca de status para Cancelado permitida ao dono só dentro da janela, ou a quem tem `tickets:cancel`; para Em Andamento ou Resolvido, só com `tickets:manage`; a edição da descrição pelo dono não muda. As checagens rodam antes de qualquer mudança no chamado.
    - `Tickets/Delete/DeleteTicketCommandHandler.cs`: passa a exigir `tickets:delete`, sem o caminho do dono.
    - `Tickets/Reopen/ReopenTicketCommand.cs`, `ReopenTicketCommandValidator.cs` e `ReopenTicketCommandHandler.cs`: motivo opcional até 500 caracteres; com motivo, grava a mensagem e o histórico `MessageAdded` no mesmo `SaveChangesAsync` da reabertura, pelos mesmos caminhos que a publicação de mensagem já usa.
    - `Tickets/GetById/TicketResponse.cs` e `GetTicketByIdQueryHandler.cs`: `CanCancel` e `OwnerCancelDeadlineUtc`.
    - `Tickets/GetOwn/OwnTicketResponse.cs` e `GetOwnTicketsQueryHandler.cs` (já alterados pela 015): os mesmos dois campos.
    - `Authorization/TicketAccessGuard.cs`: só se a regra nova couber num método dele, sem mudar o comportamento dos métodos existentes para as outras rotas.
  - Infrastructure:
    - `Roles/RolePermissionConfiguration.cs`: o Atendente e o Colaborador perdem `tickets:delete-own`; o Administrador recebe as duas novas pelo catálogo.
    - Migration nova: insere `tickets:cancel` e `tickets:delete` e os vínculos do Administrador com `ON CONFLICT DO NOTHING`; apaga todos os vínculos de `tickets:delete-own`, inclusive os concedidos à mão, e depois a permissão.
  - Web.Api:
    - `Endpoints/Tickets/Reopen.cs`: corpo opcional com o motivo.
    - `Endpoints/Tickets/Delete.cs` e `Update.cs`: sem mudança de rota; só a autorização no handler muda.
  - Testes:
    - unitários: `UpdateTicketCommandHandlerTests`, `DeleteTicketCommandHandlerTests`, `ReopenTicketCommandHandlerTests`, `GetTicketByIdQueryHandlerTests`, `GetOwnTicketsQueryHandlerTests` e o teste da política de cancelamento;
    - integração: `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` e um teste da migration de permissões.
- **Contratos:**

  | Rota | Autorização (nova regra) | Mudança | Status |
  |---|---|---|---|
  | `PUT tickets/{id}` | dono com `tickets:update-own` edita a descrição e cancela na janela; `tickets:cancel` cancela sempre; `tickets:manage` faz as outras trocas | nenhuma no payload | 204 · 400 · 403 `Tickets.CancelWindowExpired` / `Tickets.CancelForbidden` / `Tickets.StatusChangeForbidden` · 404 · 409 |
  | `DELETE tickets/{id}` | `tickets:delete` | nenhuma no payload | 204 · 403 · 404 |
  | `POST tickets/{id}/reopen` | sem mudança (dono com `tickets:reopen-own` ou `tickets:manage`) | corpo opcional com o motivo (até 500) | 204 · 400 · 403 · 404 |
  | `GET tickets/{id}` | sem mudança | + `canCancel`, `ownerCancelDeadlineUtc` | 200 · 403 · 404 |
  | `GET tickets/mine` | sem mudança | + `canCancel`, `ownerCancelDeadlineUtc` em cada item | 200 · 400 · 401 · 403 |

  **Quebra de comportamento (intencional):** o Atendente deixa de cancelar e de excluir; o dono deixa de cancelar depois de 15 minutos, de excluir e de mover o status para Em Andamento ou Resolvido. Os payloads só ganham campos.
- **Mudança no banco:** sim, só dados de permissão (duas permissões novas, uma removida, vínculos ajustados). Nenhuma coluna nova.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 2.6 (RN-06 reescrita e RN nova da janela), a seção do fluxo de status (linha 639), 4.3 (rotas e permissões) e o catálogo de permissões.
- **Análise:**
  - **Concorrência:** a reabertura com motivo grava chamado, mensagem e histórico num único `SaveChangesAsync`, sob a versão de linha do chamado; duas reaberturas simultâneas resultam numa só, e a outra recebe o conflito de hoje. O limite da janela usa o horário do servidor (`IDateTimeProvider`), nunca o do cliente.
  - **Quebra de contrato:** só de comportamento, listada acima; nenhum campo sai.
  - **Desempenho:** `canCancel` depende de uma consulta de permissão de quem chama, uma vez por requisição, e não por item; na listagem, a regra por item é só comparar datas e status.
  - **Segurança:** a regra é aplicada no handler, então o botão escondido no front não é a única barreira. A remoção de `tickets:delete-own` tira também os vínculos dados à mão, de propósito.
- **Riscos:**
  - R-1: atendentes que cancelavam chamados no dia a dia perdem essa ação até o administrador conceder `tickets:cancel`. A entrega deve ser comunicada a quem administra as funções.
  - R-2: ordem de deploy: se o back-end subir antes do front ajustado, o front mostra "Excluir" e "Cancelar" a quem já não pode, e o usuário recebe o 403. A mensagem de erro do back precisa ser clara (DEC-7).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Permissões `tickets:cancel` e `tickets:delete` no catálogo, remoção de `tickets:delete-own`, vínculos padrão e migration | AC-8 | `add-entity` | [x] |
| T-2 | Política de cancelamento (constante, prazo, regra do dono) e os três erros novos | AC-1, AC-2, AC-3, AC-14 | `add-feature` | [x] |
| T-3 | Autorização nova no `PUT tickets/{id}` (cancelar e demais trocas de status) | AC-1 a AC-6 | `add-feature` | [x] |
| T-4 | Exclusão só com `tickets:delete` | AC-7 | `add-feature` | [x] |
| T-5 | Reabertura com motivo opcional, atômica | AC-9, AC-10, AC-11 | `add-feature` | [x] |
| T-6 | `canCancel` e `ownerCancelDeadlineUtc` no detalhe e em `GET tickets/mine` | AC-12, AC-13 | `add-feature` | [x] |
| T-7 | Testes unitários e de integração da seção 6 | AC-1 a AC-14 | `add-tests` | [x] |
| T-8 | Atualizar o `SYSTEM_SPECIFICATION.md` (RN-06, RN nova, fluxo de status, 4.3 e catálogo) | AC-15 | — | [x] |
| T-9 | Revisão de convenções, build sem avisos e suíte inteira | AC-15 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Cancel_Should_ReturnNoContentAndRecordHistory_WhenOwnerCancelsWithinWindow`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_CancelTicket_WhenOwnerIsWithinCancelWindow`, `Handle_Should_CancelTicket_WhenOwnerCancelsExactlyAtDeadline` | Pela API, o chamado de Maria aberto há 14 minutos é cancelado por ela: 204, status Cancelado e um único histórico `StatusChanged` depois da abertura. O unitário usa o relógio do teste (9h00 → 9h14) e confere também o limite inclusivo (9h15). | Aprovado — 2026-10-27, execução da spec-verify |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Cancel_Should_ReturnCancelWindowExpiredAndChangeNothing_WhenOwnerCancelsAfterWindow`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_ReturnCancelWindowExpiredAndChangeNothing_WhenOwnerIsPastCancelWindow`, `Handle_Should_ReturnCancelWindowExpired_WhenOwnerAlsoManagesTicketsButIsPastWindow` | Aberto há 16 minutos (9h16): 403 `Tickets.CancelWindowExpired`, o status continua Aberto (ou Em Andamento, no unitário) e nenhum histórico novo. O unitário confere também o dono que atende a fila (sem `tickets:cancel`): o erro é o do prazo dele. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Cancel_Should_ReturnCancelWindowExpired_WhenOwnerCancelsReopenedTicketAfterWindow` | Cancelado e reaberto dentro da janela, com a abertura 20 minutos para trás: o novo cancelamento recebe 403 `Tickets.CancelWindowExpired` (a janela conta da abertura, não da reabertura). A reabertura é feita de verdade pela API. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Cancel_Should_AllowOnlyCancelPermission_WhenTicketIsTwoHoursOld`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_ReturnCancelForbiddenAndChangeNothing_WhenManagerLacksCancelPermission` | Chamado aberto há 2 horas: Ana (Atendente, sem `tickets:cancel`) recebe 403 `Tickets.CancelForbidden` e o chamado continua Aberto; o administrador recebe 204 e o chamado fica Cancelado. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Cancel_Should_ReturnNoContent_WhenRoleWasGrantedCancelPermission`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_CancelTicket_WhenCallerHasCancelPermission`, `Handle_Should_CancelTicket_WhenOwnerWithoutUpdateOwnHasCancelPermission`, `Handle_Should_ReturnUnauthorized_WhenCallerWithOnlyCancelPermissionChangesDescription`, `Handle_Should_ReturnUnauthorized_WhenCallerWithOnlyCancelPermissionMovesStatusForward` | O administrador concede `tickets:cancel` a uma role criada no teste e a dá a um usuário, que cancela um chamado de outra pessoa aberto há 2 horas: 204 e status Cancelado. Os unitários conferem ainda que essa permissão vale também para o dono sem `tickets:update-own` e que não dá direito a editar a descrição nem a mover o status para Em Andamento. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_ForbidStatusChangeButAllowDescriptionEdit_WhenOwnerUpdatesOwnTicket`, `Update_Should_ReturnNoContent_WhenOwnerUpdatesOwnTicket`; `tests/TickestPristine.Application.UnitTests/Tickets/UpdateTicketCommandHandlerTests.cs` › `Handle_Should_ReturnStatusChangeForbiddenAndChangeNothing_WhenOwnerMovesStatusForward`, `Handle_Should_UpdateDescription_WhenOwnerHasUpdateOwnPermission` | Maria tenta mudar para Em Andamento: 403 `Tickets.StatusChangeForbidden` e o status continua Aberto; depois edita só a descrição: 204 e a descrição nova no detalhe. O unitário cobre também Em Andamento → Resolvido e confere que nada foi gravado. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-7 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Delete_Should_AllowOnlyDeletePermission_WhenOwnerAgentAndAdministratorTry`; `tests/TickestPristine.Application.UnitTests/Tickets/DeleteTicketCommandHandlerTests.cs` › `Handle_Should_ReturnUnauthorizedAndKeepTicket_WhenOwnerLacksDeletePermission`, `Handle_Should_ReturnUnauthorizedAndKeepTicket_WhenManagerLacksDeletePermission`, `Handle_Should_SoftDeleteTicketAndRaiseDomainEvent_WhenCallerHasDeletePermission` | Maria e Ana recebem 403 e o chamado continua sem `DeletedAtUtc`; o administrador recebe 204 e o chamado fica excluído logicamente, com o histórico `Deleted` em nome dele. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-8 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Migration_Should_ReplaceDeleteOwnWithCancelAndDelete_IncludingManualGrants` | A migration é desfeita pelo `IMigrator` (o banco volta a ter `tickets:delete-own` com os vínculos de Administrador, Atendente e Colaborador), ganha um vínculo dado à mão a uma role do teste e é aplicada de novo: `tickets:delete-own` some de `permissions` e de `role_permissions`, e `tickets:cancel` e `tickets:delete` existem, são administrativas e estão vinculadas só ao Administrador. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-9 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Reopen_Should_PostTrimmedReasonAsMessage_WhenReasonIsGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/ReopenTicketCommandHandlerTests.cs` › `Handle_Should_ReopenAndPostTrimmedReasonAsMessage_WhenReasonIsGiven` | Maria reabre o chamado Resolvido com "  A impressora parou de novo.  ": 204, status Aberto, uma mensagem dela com o texto sem os espaços das pontas e os históricos `Reopened` e `MessageAdded`. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-10 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Reopen_Should_SkipBlankReasonAndRejectLongReason_WhenReasonIsMissingBlankOrTooLong`; `tests/TickestPristine.Application.UnitTests/Tickets/ReopenTicketCommandHandlerTests.cs` › `Handle_Should_ReopenWithoutMessage_WhenReasonIsMissingOrBlank`; `tests/TickestPristine.Application.UnitTests/Tickets/TicketValidatorsTests.cs` › `ReopenValidator_Should_HaveError_WhenReasonExceedsMaxLength`, `ReopenValidator_Should_NotHaveErrors_WhenReasonHasMaxLength` | Sem corpo e com motivo em branco: 204 sem mensagem nova; com 501 caracteres: 400, o chamado continua Resolvido e sem mensagem. O validator aceita 500 e recusa 501. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-11 | `tests/TickestPristine.Application.UnitTests/Tickets/ReopenTicketCommandHandlerTests.cs` › `Handle_Should_SaveReopeningAndReasonInSingleSaveChanges_WhenReasonIsGiven`, `Handle_Should_SaveNothing_WhenTheSingleSaveChangesFails` | Um interceptor do teste conta as gravações: a reabertura com motivo faz um único `SaveChangesAsync`, com o chamado, a mensagem e os dois históricos; quando essa gravação falha, o handler não tenta outra, e o banco continua com o chamado Resolvido, sem mensagem e sem histórico. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-12 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetById_And_GetOwn_Should_ReturnCanCancelForCaller_WithinAndAfterOwnerWindow`; `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketByIdQueryHandlerTests.cs` › `Handle_Should_ReturnCanCancelWithinOwnerWindow_WhenCallerIsOwner`, `Handle_Should_ReturnCanCancelOnlyForCancelPermission_WhenOwnerWindowHasPassed`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnCanCancelOnlyWithinOwnerWindow_WhenCallerIsOwner`, `Handle_Should_ReturnCanCancelAfterWindow_WhenCallerHasCancelPermission` | Com a abertura 10 minutos para trás, Maria vê `canCancel` = `true` no detalhe e em `GET tickets/mine`; com 20 minutos, `false` nos dois; o administrador vê `true` no detalhe. Em todos, `ownerCancelDeadlineUtc` = abertura + 15 min. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-13 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetById_Should_ReturnCannotCancel_WhenTicketIsCanceled`; `tests/TickestPristine.Application.UnitTests/Tickets/GetTicketByIdQueryHandlerTests.cs` › `Handle_Should_ReturnCannotCancel_WhenTicketIsInactive`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnCannotCancel_WhenTicketIsInactive` | Chamado Cancelado: `canCancel` = `false` para Maria e para o administrador. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-14 | `tests/TickestPristine.Application.UnitTests/Tickets/TicketCancellationPolicyTests.cs` › `OwnerDeadline_Should_BeFifteenMinutesAfterOpening`, `IsWithinOwnerWindow_Should_IncludeTheDeadline_WhenMinutesAfterOpeningAreGiven`, `CanCancel_Should_RequireActiveTicketAndPermissionOrOwnerWindow_WhenEvaluated`; verificação descrita: busca por `OwnerWindowMinutes` e pelo número 15 nos fontes | O valor 15 aparece só em `TicketCancellationPolicy.OwnerWindowMinutes`; o handler de edição, `GET tickets/{id}` e `GET tickets/mine` usam `TicketCancellationPolicy`, e a mensagem de `Tickets.CancelWindowExpired` recebe a constante por parâmetro. Os testes da política fixam o prazo, o limite inclusivo e a combinação de status, permissão e janela. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-15 | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | Build sem incremental com 0 avisos e 0 erros; 1.130 testes aprovados (50 de arquitetura, 693 unitários e 387 de integração) na execução da T-9 e de novo na da `spec-verify`. | Aprovado — 2026-10-27: build 0 avisos e 0 erros; 1.130 aprovados, 0 com falha |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx --no-incremental` em 2026-10-27: 0 avisos, 0 erros.
- **Testes:** `dotnet test TickestPristine.slnx --logger trx` em 2026-10-27, com o Docker ligado: 1.130 aprovados e 0 com falha (50 de arquitetura, 693 unitários e 387 de integração). Os testes citados na rastreabilidade aparecem como `Passed` nos relatórios TRX dessa execução.
- **Auditoria:** `python scripts/auditoria_sdd.py .` em 2026-10-27: score 100,0%, CONFORME, 0 falhas críticas, código de saída 0.
- **Revisão:** `ca-review` aplicado por um subagente revisor independente, em duas rodadas sobre a árvore de trabalho: a primeira apontou 12 itens, nenhum bloqueante, dos quais 11 foram corrigidos e 1 recusado pelo analisador do projeto (decisão 18 de `DECISIONS.md`); a segunda confirmou as correções e só apontou uma referência antiga da E-18 no `SYSTEM_SPECIFICATION.md`, também corrigida. Veredito: **aprovada, sem achados bloqueantes**. O código não mudou depois da segunda rodada.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` (na T-8, com as correções da revisão, conferido nesta verificação): 2.2 (roles e catálogo de permissões), 2.5 (quem muda o status), 2.6 (RN-06 revisada, RN-32 e RN-33 novas), 4.3.1 (rotas de edição, exclusão e reabertura), 5.4 e 5.5 (fluxos de tratar e reabrir), 8 (E-18) e 9.5 (Q-03 respondida).
- **CI:**
- **Entrega:** PR [#33](https://github.com/rafaelps15/tickest-pristine/pull/33), mergeado na `main` em 2026-10-28. O número foi registrado depois do merge, numa branch de documentação, porque o campo ficou vazio no PR.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end, separada da 015 (DEC-4 da 015). |
| 2026-10-27 | Em descoberta | Duas rodadas de perguntas e uma pergunta extra respondidas no chat (DEC-1 a DEC-10). Aguardando aprovação. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("vamos aprovar as duas"), junto com a 015. Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Implementação só depois do merge da 015 (DEC-8). |
| 2026-10-27 | Em implementação | `/spec-implement 016` na branch `feat/SPEC-016-cancel-window-delete-reopen-reason`, criada a partir da `origin/main` com a 015 mergeada (PR #32), conforme a DEC-8. Rafael Santos pediu a execução contínua de todas as tarefas, sem commits (as mudanças ficam na árvore de trabalho para revisão), com as escolhas técnicas registradas em `DECISIONS.md`, nesta pasta, e uma revisão final por um subagente independente. |
| 2026-10-27 | Em implementação | T-1 a T-9 implementadas, sem commits (pedido de Rafael Santos: as mudanças ficam na árvore de trabalho). Escolhas técnicas em `DECISIONS.md`, nesta pasta, todas dentro dos REQs, ACs e DECs aprovados. Revisão por um subagente independente em duas rodadas: a primeira apontou 12 itens, nenhum bloqueante (comentários fora do lugar, referências desatualizadas no `SYSTEM_SPECIFICATION.md`, robustez do teste da migration, três caminhos sem teste unitário, nomes de teste); 11 foram corrigidos e 1 não foi aplicado porque o analisador do projeto (`IDE0047`) o recusa (decisão 18). A segunda rodada não encontrou nada restante. Build com 0 avisos; 1.130 testes aprovados; auditoria SDD com código de saída 0. Próximo passo: `spec-verify`. |
| 2026-10-27 | Verificada | `spec-verify` aprovada: build com 0 avisos; 1.130 testes aprovados e 0 com falha; auditoria SDD 100% (código de saída 0); AC-1 a AC-15 cobertos por testes que exercitam o comportamento descrito e passaram nesta execução (AC-14 também pela verificação descrita: o 15 só existe em `TicketCancellationPolicy.OwnerWindowMinutes`); revisão independente sem achados bloqueantes; `SYSTEM_SPECIFICATION.md` conferido. O código continua sem commit, a pedido de Rafael Santos. Riscos que continuam: R-1 (atendentes perdem cancelar e excluir até receber `tickets:cancel` e `tickets:delete`) e R-2 (ordem de deploy com o front); as permissões novas são administrativas (decisão 5 de `DECISIONS.md`). |
