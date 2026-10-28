---
id: 014
titulo: Troca de senha no primeiro acesso, chamados em aberto por atendente e devolução à fila na desativação
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-014-first-access-open-tickets
pr: "#34"                     
---

# 014 — Troca de senha no primeiro acesso, chamados em aberto por atendente e devolução à fila na desativação

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-27: "Spec para o back-end (`tickest-pristine`)", com três itens: (1) cadastro com troca de senha obrigatória no primeiro acesso; (2) quantos chamados em aberto um usuário atende, para o aviso de desativação ("Atende N chamados em aberto. Eles vão ficar sem responsável"), mais o filtro por responsável em `GET /tickets` para o link "ver esses chamados"; (3, opcional) o que acontece com esses chamados ao desativar. Pediu "só com regras", sem código. Em seguida: "veja qual é a proxima e acrescente lá".
- **Fonte:** Claude Doc "Spec back-end — Gestão de usuários (tickest-pristine)", https://claude.ai/artifact/DBLRbtJqZXhgCAh7BE5eh9 (escrito no mesmo chat).
- **Referências:**
  - `SYSTEM_SPECIFICATION.md`: 2.6 (RN-16, senha provisória do administrador inicial), 4.6 (senhas e login), 5.1 (primeiro acesso).
  - Spec 008 (convite e ativação, em `Rascunho`): propõe tirar a senha do cadastro. Ver DEC-1.
  - Spec 010 (redefinição de senha pelo administrador, em `Rascunho`): usa o mesmo fluxo de troca obrigatória.
  - Spec 001 (remover o responsável, `Entregue`): regra de histórico e evento ao tirar o responsável de um chamado.
  - Spec 021 (`Descartada`): tratava só da devolução à fila na desativação; incorporada a esta spec (DEC-9), com a regra de status desta (DEC-4).
  - Código: `Application/Users/Register/RegisterUserCommandHandler.cs`, `Users/Login/LoginUserCommandHandler.cs`, `Users/ChangeRequiredPassword/*`, `Users/UserPasswords.cs`, `Domain/Users/UserCredential.cs`, `Users/GetAll/*`, `Users/Deactivate/DeactivateUserCommandHandler.cs`, `Tickets/GetAll/*`, `Tickets/Unassign/*`, `Web.Api/Endpoints/Tickets/GetAll.cs`, `Domain/Tickets/TicketStatus.cs`.
- **Fatos do repositório que afetam o pedido:**
  - O cadastro cria a credencial sem a marcação de troca obrigatória (`RegisterUserCommandHandler.cs:38`). Hoje só o seed do administrador a liga.
  - **Atualização de 2026-10-27, antes da implementação:** a spec 008 já foi mergeada na `main` (PR #30). O cadastro não recebe mais senha (`RegisterUserCommand(Email, FirstName, LastName)`) nem cria credencial; a credencial nasce na ativação por convite, com a senha escolhida pelo próprio usuário. Pela DEC-1 (a), o item 1 (REQ-1 a REQ-3) sai junto: ver a linha do histórico da mesma data.
  - O login já recusa a entrada quando a marcação está ligada (`LoginUserCommandHandler.cs:39`, 403 `Users.PasswordChangeRequired`), e `POST users/login/new-password` já troca a senha e desliga a marcação (`UserPasswords.cs:25`).
  - **O filtro por responsável em `GET /tickets` já existe:** a consulta tem `AssignedToUserId` (`Tickets/GetAll/GetTicketsQuery.cs`) e o endpoint o expõe como `assignedToUserId` (`Web.Api/Endpoints/Tickets/GetAll.cs:21`). Quando `unassignedOnly` está ligado, o filtro de responsável é ignorado. O item 2c do pedido não exige mudança no back-end; ver REQ-4.
  - "Em aberto" no domínio = `Open` e `InProgress`; os demais status são `Resolved`, `Closed` e `Canceled` (`TicketStatus.cs`).
  - O item da listagem de usuários (`UserSummaryResponse`) não tem contagem de chamados.
  - A desativação não mexe nos chamados do usuário; `UserDeactivatedDomainEvent` não tem tratador.
  - `Ticket` tem token de concorrência no `xmin` (`TicketConfiguration.cs`); um conflito vira 409 no `GlobalExceptionHandler`, sem nada salvo.
  - O histórico de remoção manual usa `TicketHistoryAction.Unassigned = 11`, com o id do responsável anterior em `OldValue`; a consulta do histórico resolve o nome desse id (`TicketHistories/GetAll/GetTicketHistoryQueryHandler.cs`) e o texto vem de `TicketHistories/TicketHistoryDescriptions.cs`. O último valor de `TicketHistoryAction` é `Assigned = 13`.
  - As transições manuais não permitem `InProgress` → `Open` (`Tickets/TicketStatusTransitions.cs`): de `InProgress` só se vai para `Resolved` ou `Canceled`. Ver DEC-5.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | **Não se aplica (DEC-1 (a); spec 008 entregue).** Todo usuário criado por `POST users/register` nasce com a credencial marcada para troca obrigatória de senha. A senha definida por quem cadastra passa a ser provisória. | pedido (item 1) |
| REQ-2 | **Não se aplica (DEC-1 (a); spec 008 entregue).** No primeiro login com a senha provisória, o usuário recebe a mesma recusa que o login já dá para troca obrigatória; depois de trocar em `POST users/login/new-password`, a provisória deixa de valer e o login volta ao normal. Nenhuma regra do fluxo de troca muda. | pedido (item 1) |
| REQ-3 | **Não se aplica (DEC-1 (a); spec 008 entregue).** O contrato de `POST users/register` não muda: recebe e-mail, nome, sobrenome e senha e devolve o id. Usuários já cadastrados e o seed do administrador não são alterados. | pedido (item 1) |
| REQ-4 | `GET /tickets` continua aceitando o filtro opcional `assignedToUserId`, que já existe; esta spec só o cobre com teste de aceitação para o uso do aviso (Aberto ou Em Andamento de um responsável). | pedido (item 2); fato do repositório |
| REQ-5 | O back-end informa, por usuário, quantos chamados em `Open` ou `InProgress` têm esse usuário como responsável. Vem num campo inteiro de cada item de `GET /users`. | pedido (item 2); DEC-2 |
| REQ-6 | A contagem considera só `Open` e `InProgress`; usuário sem chamados nessa situação tem contagem 0, nunca ausente ou nula. | pedido (item 2) |
| REQ-7 | Na mesma transação da desativação, todo chamado `Open` ou `InProgress` em que o usuário é o responsável fica sem responsável, com o mesmo evento que a remoção manual de responsável já produz (spec 001) e uma linha de histórico própria, que identifica a remoção automática por desativação, com o responsável anterior e quem desativou; ela aparece como "Responsável removido automaticamente (usuário desativado): <nome>". | pedido (item 3); DEC-4; DEC-6 |
| REQ-8 | Nessa mesma transação, os chamados `InProgress` devolvidos voltam para `Open`, com o registro de histórico e o evento de mudança de status. Os que já estavam `Open` não mudam de status. Chamados `Resolved`, `Closed` e `Canceled` mantêm o responsável e o status. Reativar o usuário não devolve os chamados a ele. | pedido (item 3); DEC-4; DEC-5 |
| REQ-9 | A volta `InProgress` → `Open` é exclusiva da desativação: as transições manuais de status continuam como estão. | DEC-5 |
| REQ-10 | Desativação, remoções de responsável e mudanças de status acontecem juntas ou nenhuma, no mesmo `SaveChangesAsync`. Se a desativação for recusada (própria conta, RN-17, último administrador, já desativado), nenhum chamado muda. Um conflito de concorrência num chamado devolve 409 e nada é salvo. | spec 021 (REQ-4); DEC-9 |
| REQ-11 | A regra vale para qualquer usuário desativado com chamados em aberto atribuídos, não só para quem tem a role Atendente. Chamados excluídos não são tocados. | spec 021 (REQ-6); DEC-7 |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | **Não se aplica (DEC-1 (a)).** REQ-1 | administrador autenticado | `POST users/register` com e-mail, nome, sobrenome e senha válidos | 200 com o id; a credencial gravada do novo usuário tem a marcação de troca obrigatória ligada |
| AC-2 | **Não se aplica (DEC-1 (a)).** REQ-2 | usuário recém-cadastrado | login com a senha definida no cadastro | 403 `Users.PasswordChangeRequired`; nenhum token emitido |
| AC-3 | **Não se aplica (DEC-1 (a)).** REQ-2 | usuário recém-cadastrado | `POST users/login/new-password` com a senha provisória e uma nova senha válida, e depois login com cada uma | troca bem-sucedida; login com a nova senha emite tokens; login com a provisória falha como credenciais inválidas |
| AC-4 | **Não se aplica (DEC-1 (a)).** REQ-3 | — | compara requisição e resposta de `POST users/register` antes e depois | mesmos campos de entrada e mesma resposta; nenhum usuário já existente nem o administrador do seed tem a marcação alterada |
| AC-5 | REQ-4 | um atendente com 2 chamados `Open`, 1 `InProgress` e 1 `Closed` | `GET /tickets?assignedToUserId=<id>&status=Open&status=InProgress` (o filtro de status é o parâmetro `status` repetido) | 200 com exatamente os 3 chamados em aberto dele; o total da paginação é 3 |
| AC-6 | REQ-5, REQ-6 | o mesmo atendente do AC-5 e um usuário sem chamados | consulta a contagem (DEC-2) | contagem 3 para o atendente e 0 para o outro usuário |
| AC-7 | REQ-5 | página de `GET /users` com vários usuários | lista a página | cada item traz a contagem; a contagem da página inteira é obtida numa única consulta ao banco, não uma por usuário |
| AC-8 | REQ-7, REQ-8, REQ-11 | atendente responsável por 1 chamado `Open`, 1 `InProgress`, 1 `Closed` e 1 `Open` excluído | desativa o atendente | 204; os 2 chamados em aberto ficam sem responsável e com status `Open`; cada um tem uma entrada de histórico de remoção automática, e o que estava `InProgress` também uma de mudança de status para `Open`; o `Closed` mantém responsável e status; o excluído não é tocado; os 2 aparecem na fila sem responsável de `GET /tickets` com status `Open` |
| AC-9 | REQ-10 | um chamado em aberto do atendente alterado por outra transação entre a leitura e a gravação da desativação | desativa o atendente | 409; o usuário continua ativo e nenhum chamado perde o responsável nem muda de status |
| AC-10 | REQ-8 | atendente desativado cujos chamados voltaram à fila | reativa o atendente | os chamados continuam sem responsável e com status `Open` |
| AC-11 | REQ-9 | chamado `InProgress` de um atendente ativo | tenta mudar o status manualmente para `Open` | a recusa é a mesma de hoje para transição inválida; status inalterado |
| AC-12 | REQ-7 | atendente sem chamados em aberto | desativa o atendente | 204; nenhum chamado alterado |
| AC-13 | REQ-7 | depois do AC-8 | `GET tickets/{id}/history` de cada chamado devolvido | a linha nova tem a ação de remoção automática, `changedByUserId` = quem desativou e a descrição "Responsável removido automaticamente (usuário desativado): <nome do atendente>" |
| AC-14 | REQ-10 | atendente com chamados em aberto e desativação recusada (quem desativa não pode administrar o privilégio dele, RN-17, ou ele é o último administrador ativo) | tenta desativar | o erro de hoje; nenhum chamado perde o responsável nem ganha linha de histórico |
| AC-15 | REQ-10 | atendente com chamados em aberto (teste unitário do handler) | desativa o atendente | usuário, chamados e históricos são gravados numa única chamada de `SaveChangesAsync` |
| AC-16 | REQ-11 | usuário sem `tickets:manage` ainda responsável por um chamado `Open` atribuído antes de perder a permissão | desativa o usuário | o chamado fica sem responsável, com a linha de histórico de remoção automática |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | A spec 008 (em `Rascunho`) tira a senha do cadastro e usa convite por e-mail. Como esta spec convive com ela? | (a) esta spec entra agora como passo intermediário; quando a 008 for feita, o cadastro deixa de receber senha e esta regra sai junto; (b) descartar esta parte e priorizar a 008; (c) fundir as duas | (a) — reaproveita o fluxo pronto, não muda contrato e não exige envio de e-mail, que o sistema ainda não tem | Rafael Santos, 2026-10-27, no chat ("pode aprovar", aceitando a recomendação) |
| DEC-2 | Como o front recebe a contagem de chamados em aberto? | (a) campo inteiro novo em cada item de `GET /users`, calculado numa consulta agregada por página; (b) endpoint dedicado com a contagem de um usuário, chamado ao abrir a confirmação | (a) — uma consulta por página, campo aditivo, e o front já tem o número ao abrir a confirmação | Rafael Santos, 2026-10-27, no chat ("pode aprovar", aceitando a recomendação) |
| DEC-3 | O filtro por responsável precisa mudar? | (a) não: `assignedToUserId` já existe e o front passa a usá-lo; (b) mudar a regra de quando `unassignedOnly` e `assignedToUserId` vêm juntos (hoje o responsável é ignorado) | (a) — sem mudança de comportamento | Rafael Santos, 2026-10-27, no chat ("pode aprovar", aceitando a recomendação) |
| DEC-4 | Decisão de produto: ao desativar um atendente, os chamados em aberto dele continuam com ele ou voltam para a fila sem responsável? | (a) manter (situação de hoje); (b) devolver à fila na desativação | (b), com status de volta a Aberto | Rafael Santos, 2026-10-27, no chat: "Ao desativar um atendente os chamados dele devem voltar para o status de aberto aguardando alguém pra resolver" |
| DEC-5 | As transições manuais não permitem `InProgress` → `Open`. Como a desativação faz essa volta? | (a) exceção só dentro da desativação, sem mudar as transições manuais; (b) liberar `InProgress` → `Open` para todos | (a) — atende a DEC-4 sem ampliar o que atendentes e administradores podem fazer à mão | Claude, 2026-10-27, como consequência direta da DEC-4; informado a Rafael Santos no chat |
| DEC-6 | Como marcar no histórico que a remoção foi automática? | (a) ação nova `UnassignedOnUserDeactivation = 14` em `TicketHistoryAction`, com texto próprio; (b) reaproveitar `Unassigned` com um marcador em `NewValue`; (c) reaproveitar `Unassigned` sem distinção | (a) — explícito no dado e no texto, sem valor mágico; o front que lista as ações precisa conhecer a nova | spec 021 (DEC-2), incorporada por Rafael Santos no chat em 2026-10-27: "pode aproveitar a 21 ok" |
| DEC-7 | A regra vale só para quem tem a role Atendente? | (a) qualquer usuário com chamados em aberto atribuídos; (b) só Atendentes | (a) — o que importa é o chamado ter um responsável que não pode mais agir | spec 021 (DEC-5), incorporada por Rafael Santos no chat em 2026-10-27: "pode aproveitar a 21 ok" |
| DEC-8 | Como alterar os chamados? | (a) carregar os chamados rastreados, alterar e gravar o histórico no mesmo `SaveChanges` (passa por auditoria, eventos e concorrência); (b) `ExecuteUpdateAsync` em lote | (a) — (b) pularia auditoria, histórico, eventos e o `xmin`; o volume por atendente é pequeno | spec 021 (DEC-6), incorporada por Rafael Santos no chat em 2026-10-27: "pode aproveitar a 21 ok" |
| DEC-9 | As specs 014 e 021 tratam da mesma desativação com regras opostas de status. Qual vale? | (a) a 014 (`InProgress` volta a `Open`), absorvendo o restante da 021; (b) a 021 (status não muda) | (a) — a 021 é descartada; dela entram a ação de histórico própria, a atomicidade com 409 e a regra para qualquer usuário | Rafael Santos, 2026-10-27, no chat: "Volta para que seja alocado outro responsável" e "pode aproveitar a 21 ok" |

## 5. Fora do escopo
- Mudanças no front (ligar a frase do primeiro acesso e ajustar o texto do aviso).
- Convite por e-mail e cadastro sem senha (spec 008).
- Redefinição de senha pelo administrador (spec 010).
- Validade para a senha provisória.
- Reatribuição automática dos chamados para outro atendente.
- Mudar as transições manuais de status.
- Marcar para troca obrigatória usuários cadastrados antes desta spec.

## 6. Plano técnico
- **Arquivos a criar ou alterar:**
  - Item 1: `Application/Users/Register/RegisterUserCommandHandler.cs` (credencial nasce marcada); testes do cadastro e de integração do login.
  - Item 2: `Application/Users/GetAll/GetUsersQueryHandler.cs` e `UserSummaryResponse.cs` (DEC-2).
  - Item 2c: só testes de `GET /tickets` (DEC-3).
  - Item 3: `Application/Users/Deactivate/DeactivateUserCommandHandler.cs`, carregando os chamados rastreados (DEC-8) e reaproveitando os eventos da remoção de responsável (`Tickets/Unassign/*`) e da mudança de status; `TicketStatusTransitions.cs` não muda (DEC-5). Ação nova `UnassignedOnUserDeactivation = 14` em `Domain/Tickets/TicketHistoryAction.cs`, com a montagem em `TicketHistories/TicketHistoryEntries.cs`, o texto em `TicketHistories/TicketHistoryDescriptions.cs` e a resolução do nome do `OldValue` em `TicketHistories/GetAll/GetTicketHistoryQueryHandler.cs` (DEC-6).
- **Contratos:** `POST users/register` sem mudança. Campo inteiro novo e aditivo em cada item de `GET /users`, com a contagem de chamados em aberto (nome definido na implementação, seguindo o padrão dos campos existentes). `GET /tickets` sem mudança. `PUT users/{id}/deactivate` sem mudança de contrato; muda o efeito nos chamados. `GET tickets/{id}/history` pode trazer a ação nova `14`, com `oldValue` = id do responsável anterior e `newValue` nulo.
- **Mudança no banco:** não prevista. A implementação confere se os índices existentes atendem a consulta por responsável e status; se faltar índice, a necessidade é registrada nesta spec antes de criar a migration.
- **Riscos:**
  - Conflito com a spec 008 se as duas avançarem sem a DEC-1.
  - Contagem calculada por linha deixaria a listagem lenta; o AC-7 cobre.
  - Desativar um atendente com muitos chamados gera muitas entradas de histórico e eventos na mesma transação.
  - Chamados `InProgress` voltando a `Open` mudam os números dos painéis por status; os cronômetros de SLA seguem as regras atuais das specs 005 e 006.
- **Impacto na documentação de estado do sistema:** `SYSTEM_SPECIFICATION.md` 2.6 (RN-16 passa a valer para todo usuário cadastrado), 4.3.1 (resposta de `GET /users`) e a regra de desativação.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | ~~Cadastro cria a credencial com troca obrigatória~~ — não se aplica (DEC-1 (a); spec 008 entregue) | AC-1, AC-4 | add-feature | — |
| T-2 | ~~Testes do primeiro acesso de usuário cadastrado~~ — não se aplica (DEC-1 (a); spec 008 entregue) | AC-2, AC-3 | add-tests | — |
| T-3 | Teste de aceitação do filtro por responsável com status em aberto | AC-5 | add-tests | [x] |
| T-4 | Contagem de chamados em aberto em `GET /users`, com consulta agregada por página | AC-6, AC-7 | add-feature | [x] |
| T-5 | Ação de histórico de remoção automática, montagem, texto e resolução do nome | AC-13 | add-feature | [x] |
| T-6 | Desativação devolve os chamados em aberto à fila, com status `Open`, histórico e eventos, no mesmo `SaveChanges` | AC-8 a AC-10, AC-12, AC-14 a AC-16 | add-feature | [x] |
| T-7 | Testes da desativação e da transição manual inalterada | AC-8 a AC-16 | add-tests | [x] |
| T-8 | Atualizar `SYSTEM_SPECIFICATION.md` | AC-1, AC-6, AC-8 | — | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | — | Não se aplica (DEC-1 (a); spec 008 entregue). | Não se aplica (DEC-1 (a)) |
| AC-2 | — | Não se aplica (DEC-1 (a); spec 008 entregue). | Não se aplica (DEC-1 (a)) |
| AC-3 | — | Não se aplica (DEC-1 (a); spec 008 entregue). | Não se aplica (DEC-1 (a)) |
| AC-4 | — | Não se aplica (DEC-1 (a); spec 008 entregue). | Não se aplica (DEC-1 (a)) |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetAll_Should_ReturnOnlyOpenTicketsOfAssignee_WhenFilteringByAssigneeAndOpenStatuses` | Atendente com 2 Abertos, 1 Em Andamento e 1 Fechado (mais 1 chamado de outro atendente); o filtro `assignedToUserId` + `status=1&status=2` devolve exatamente os 3 em aberto, com `totalCount` 3. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-6 | `tests/TickestPristine.Application.UnitTests/Users/GetUsersQueryHandlerTests.cs` › `Handle_Should_CountOnlyOpenAndInProgressAssignedTickets_WhenListingUsers`; `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAll_Should_ReturnOpenTicketCountInSingleQuery_WhenListingUsers` | Unitário: 3 para quem tem 2 Abertos e 1 Em Andamento (ignorando Resolvido, Fechado, Cancelado e excluído), 0 para quem não tem chamados. Integração: `openTicketCount` 3 e 0 na resposta de `GET users`. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-7 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `GetAll_Should_ReturnOpenTicketCountInSingleQuery_WhenListingUsers` | Com o `EfCommandCounter` durante o `GET users` com dois usuários na página: um único comando lê `public.tickets`, e esse comando também lê `public.users`. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-8 | `tests/TickestPristine.Application.UnitTests/Users/DeactivateUserCommandHandlerTests.cs` › `Handle_Should_ReturnOpenTicketsToQueue_WhenUserHasOpenTickets`; `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `Deactivate_Should_ReturnOpenTicketsToQueue_WhenUserIsAssignedToOpenTickets` | Aberto e Em Andamento ficam sem responsável e Abertos, com a remoção automática no histórico e o `StatusChanged` só no que estava Em Andamento; Resolvido, Fechado, Cancelado e excluído não mudam; os 2 aparecem em `GET tickets?unassignedOnly=true&status=Open`. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-9 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `Deactivate_Should_ReturnConflictAndSaveNothing_WhenTicketChangesDuringDeactivation` | Uma transação do teste passa o chamado para outro atendente sem confirmar; a desativação lê o chamado ainda com o responsável antigo, espera a linha e, com a confirmação, responde 409; o usuário continua ativo, o chamado fica com o outro atendente e sem remoção automática no histórico. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-10 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `Activate_Should_KeepTicketsInQueue_WhenDeactivatedUserIsReactivated` | Depois de desativar e reativar, o chamado continua na fila sem responsável, Aberto, e nenhum chamado volta para o usuário. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-11 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Update_Should_ReturnBadRequest_WhenManagerMovesInProgressTicketBackToOpen` | `PUT tickets/{id}` de Em Andamento para Aberto responde 400 `Tickets.InvalidStatusTransition`, e o status continua Em Andamento. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-12 | `tests/TickestPristine.Application.UnitTests/Users/DeactivateUserCommandHandlerTests.cs` › `Handle_Should_ChangeNoTicket_WhenUserHasNoOpenTickets` | Usuário só com chamados Resolvido, Fechado e Cancelado (e outro atendente com um Em Andamento): a desativação tem sucesso e nenhum chamado muda de responsável ou status, sem eventos nem histórico. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-13 | `tests/TickestPristine.Application.UnitTests/TicketHistories/TicketHistoryEntriesTests.cs` › `UnassignedOnUserDeactivation_Should_RecordPreviousAssigneeWithoutNewValue`; `tests/TickestPristine.Application.UnitTests/TicketHistories/TicketHistoryDescriptionsTests.cs` › `For_Should_NameDeactivatedAssignee_WhenUnassignedOnUserDeactivation`; `tests/TickestPristine.Application.UnitTests/TicketHistories/GetTicketHistoryQueryHandlerTests.cs` › `Handle_Should_NameDeactivatedAssignee_WhenUnassignedOnUserDeactivation`; `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `Deactivate_Should_ReturnOpenTicketsToQueue_WhenUserIsAssignedToOpenTickets` | A linha tem a ação 14, o id do anterior em `oldValue`, quem desativou como autor e a descrição "Responsável removido automaticamente (usuário desativado): Ana Souza", lida por `GET tickets/{id}/history`. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-14 | `tests/TickestPristine.Application.UnitTests/Users/DeactivateUserCommandHandlerTests.cs` › `Handle_Should_KeepTicketsAssigned_WhenDeactivationIsRefused`; `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `Deactivate_Should_KeepTicketsAssigned_WhenDeactivationIsForbidden` | Último administrador (unitário) e RN-17 (integração, 403): o chamado continua com o responsável e o status, sem eventos nem linha de histórico. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-15 | `tests/TickestPristine.Application.UnitTests/Users/DeactivateUserCommandHandlerTests.cs` › `Handle_Should_SaveUserAndTicketsInSingleSaveChanges_WhenUserHasOpenTickets` | Conta as gravações pelo evento `SavedChanges` do contexto: uma só para usuário, chamado e histórico. | Aprovado em 2026-10-27 (execução da `spec-verify`) |
| AC-16 | `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` › `Deactivate_Should_ReturnTicketToQueue_WhenUserNoLongerManagesTickets` | Colaboradora (sem `tickets:manage`) responsável por um chamado Aberto: ao ser desativada, o chamado vai para a fila com a remoção automática no histórico. | Aprovado em 2026-10-27 (execução da `spec-verify`) |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx --no-incremental` em 2026-10-27: 0 avisos, 0 erros.
- **Testes:** `dotnet test TickestPristine.slnx --logger trx` em 2026-10-27: 1.097 aprovados, 0 com falha (ArchitectureTests 50, Application.UnitTests 664, IntegrationTests 383); os 16 testes da rastreabilidade aparecem como `Passed` no relatório por teste. Auditoria `python scripts/auditoria_sdd.py .`: score 100,0%, CONFORME, 0 falhas críticas, código de saída 0.
- **Revisão:** `ca-review` sobre os 15 arquivos alterados da spec: sem bloqueantes, sem violações de convenção e sem lacunas de teste (pronto para commit). Antes, duas rodadas de revisão independente; os achados da primeira foram corrigidos (`DECISIONS.md`, itens 15 a 18 e 23 a 24) e a segunda não encontrou nada. Fora do escopo, não revisadas: as pastas não versionadas das specs 017 a 020, 022 e 023 e `specs/_fontes/`.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`: 2.6 (RN-31, ação 14; RN-32 nova), 2.7 (C-05, C-18, C-20), 4.4 (enums de negócio), 5.8 (desativação) e 7.2 (GAP-18 atualizada; GAP-19 e GAP-20 novas).
- **CI:**
- **Entrega:** PR #34 (https://github.com/rafaelps15/tickest-pristine/pull/34); a spec fica `Entregue` quando o responsável mergear o PR na `main`. Avisar o front da ação 14 no histórico (`UnassignedOnUserDeactivation`), do campo `openTicketCount` em `GET users` e do novo efeito da desativação (texto do aviso). O item 1 não se aplica (DEC-1 (a)).

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir do pedido de Rafael Santos no chat ("veja qual é a proxima e acrescente lá"); próximo número livre depois da 013. |
| 2026-10-27 | Em descoberta | Descoberta iniciada: fatos do repositório levantados (filtro `assignedToUserId` já existe; desativação não mexe nos chamados; conflito de princípio com a spec 008). Aguardando DEC-1 a DEC-4 e a confirmação dos `[SUPOSIÇÃO]`. |
| 2026-10-27 | Em descoberta | DEC-1, DEC-2 e DEC-3 decididas por Rafael Santos ("pode aprovar", aceitando as recomendações). DEC-4 decidida por ele: chamados em aberto voltam à fila com status Aberto. DEC-5 registrada como consequência da DEC-4. REQ-7 a REQ-9 e AC-8 a AC-12 reescritos; nenhum `[SUPOSIÇÃO]` pendente. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat: "pode aprovar". |
| 2026-10-27 | Aprovada | Conflito com a spec 021 (DEC-3 dela: `InProgress` não volta a `Open`) resolvido por Rafael Santos no chat a favor da DEC-4 desta spec: "Volta para que seja alocado outro responsável". O item 3 (REQ-7 a REQ-9) é implementado aqui. |
| 2026-10-27 | Em descoberta | Revisão para incorporar a spec 021 (DEC-9): REQ-7 passa a ter linha de histórico própria (DEC-6); REQ-10 e REQ-11 novos; AC-8 e AC-9 reescritos; AC-13 a AC-16 novos; DEC-6 a DEC-9; tarefas renumeradas (T-5 a T-8). REQs novos e a ação 14 no histórico (contrato) levam a spec de volta a `Em descoberta`. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat: "pode aproveitar a 21 ok". Nenhum `[SUPOSIÇÃO]` pendente. Começa depois que a spec 016 for concluída, a pedido dele: "quando finalizar de desenvolver a 16 vc inicia a 14 automaticamente". |
| 2026-10-27 | Aprovada | Revisão antes da implementação, sem transição: a spec 008 já está na `main` (PR #30), então, pela DEC-1 (a) já aprovada ("quando a 008 for feita, o cadastro deixa de receber senha e esta regra sai junto"), REQ-1 a REQ-3, AC-1 a AC-4, T-1 e T-2 passam a "não se aplica". AC-5 corrigido para o nome real do parâmetro (`status` repetido), sem mudar o critério. Informado a Rafael Santos no chat. |
| 2026-10-27 | Em implementação | `/spec-implement 014` iniciado na branch `feat/SPEC-014-first-access-open-tickets`, criada a partir da `origin/main` (`b020762`) depois que a spec 016 foi concluída. Por pedido de Rafael Santos, sem commits por tarefa: as mudanças ficam na árvore de trabalho para a revisão dele. |
| 2026-10-27 | Em implementação | Tarefas T-3 a T-8 concluídas, sem commits (pedido do responsável). Revisão independente feita; correções aplicadas (testes do AC-12 e de Resolvido/Cancelado, teste do AC-9 sem depender de tempo, `EfCommandCounter` no AC-7). Duas lacunas registradas para decisão do responsável no `SYSTEM_SPECIFICATION.md` (GAP-19 e GAP-20) e no `DECISIONS.md` desta pasta. Próximo passo: `spec-verify`. |
| 2026-10-27 | Verificada | `spec-verify`: build sem avisos, 1.097 testes aprovados (os 16 da rastreabilidade, conferidos no relatório por teste), auditoria 100% com saída 0 e `ca-review` sem achados. AC-5 a AC-16 cobertos; AC-1 a AC-4 não se aplicam (DEC-1 (a)). Pendências do responsável, sem bloquear a entrega: GAP-19 e GAP-20. |
