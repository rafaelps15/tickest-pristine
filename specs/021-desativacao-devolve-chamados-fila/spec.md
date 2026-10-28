---
id: 021
titulo: Chamados em aberto de um usuário desativado voltam para a fila sem responsável
status: Descartada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-021-unassign-tickets-on-user-deactivation                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 021 — Chamados em aberto de um usuário desativado voltam para a fila sem responsável

> **Decisão de produto (DEC-1): opção (b).** Os chamados em aberto do usuário desativado voltam para a fila sem responsável.

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-27 (item 5 de 5): "Decisão de produto: o que acontece com os chamados em aberto de um atendente desativado. (a) Continuam atribuídos a ele, que é o comportamento de hoje e o que o aviso do front descreve. (b) Voltam para a fila sem responsável, e nesse caso o caso de uso de desativação faz a remoção e registra no histórico. Se for (b), o front só ajusta o texto do aviso." Na primeira versão: "No caso de uso de desativação de usuário/atendente, remover a atribuição dos chamados em aberto (`AssignedToUserId = null`). Registrar evento no histórico/linha do tempo dos chamados afetados informando a desatribuição automática por desativação do atendente. No frontend, atualizar o texto de confirmação/aviso do modal de desativação."
- **Referências:** `SYSTEM_SPECIFICATION.md` 2.6 RN-17 e RN-19 (desativação), RN-30 (convites), 2.7 C-18 (histórico), 5.10 (atribuir e remover responsável); spec 001 (remover responsável) e spec 009 (elegibilidade de responsável); spec 015 (última atividade).
- **Fatos do repositório:**
  - `Users/Deactivate/DeactivateUserCommandHandler.cs`: recusa a própria conta, usuário inexistente e já desativado; passa pelo `PrivilegeEscalationGuard` (RN-17) e pelo `AdministratorGuard` (último administrador); marca `IsActive = false` e `DeactivatedAtUtc`; revoga convites e sessões; dispara `UserDeactivatedDomainEvent` (sem handlers hoje); um único `SaveChangesAsync`; depois invalida o cache de permissões. **Não toca nos chamados.**
  - `Tickets/Unassign/UnassignTicketCommandHandler.cs` (spec 001): só em chamado ativo (`TicketStatusTransitions.IsActive`: `Open` e `InProgress`); põe `AssignedToUserId = null`, **não muda o status**, dispara `TicketUnassignedDomainEvent(TicketId, PreviousAssignedToUserId, ChangedByUserId)` (sem handlers) e grava `TicketHistoryEntries.Unassigned(...)` (`TicketHistoryAction.Unassigned = 11`, `OldValue` = id do anterior), no mesmo `SaveChangesAsync`.
  - `TicketHistoryDescriptions.cs:35`: `Unassigned` aparece como "Responsável removido: <nome>"; a consulta do histórico resolve o nome do `OldValue` por subconsulta (C-18), inclusive de usuário desativado.
  - `TicketAssigneeEligibility.cs`: só usuário ativo com `tickets:manage` pode **receber** chamado; um usuário desativado deixa de aparecer como atendente, mas os chamados que já tinha continuam com ele.
  - `Ticket` tem token de concorrência no `xmin` (`TicketConfiguration.cs:39-41`); um conflito vira 409 no `GlobalExceptionHandler` ("Atualize a tela e tente novamente"), sem nada salvo.
  - Na branch da 015, o interceptor de última atividade atualiza o `LastActivityAtUtc` de todo chamado que ganha uma linha de histórico no mesmo `SaveChanges` (015 DEC-6/DEC-11).
  - `GET tickets?assignedToUserId={id}&status=Open&status=InProgress&pageSize=1` (`tickets:manage`) já devolve, no `totalCount`, quantos chamados em aberto o usuário tem.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Ao desativar um usuário (`PUT users/{id}/deactivate`), todo chamado ativo (`Open` ou `InProgress`, não excluído) atribuído a ele fica sem responsável. Chamados resolvidos, fechados ou cancelados mantêm o responsável. | pedido (b) |
| REQ-2 | O status do chamado não muda: um chamado Em Andamento continua Em Andamento, sem responsável, como na remoção manual (spec 001). | DEC-3 |
| REQ-3 | Cada chamado afetado ganha uma linha de histórico própria, que identifica a remoção automática por desativação, com o responsável anterior e quem desativou; ela aparece como "Responsável removido automaticamente (usuário desativado): <nome>". | pedido (b); DEC-2 |
| REQ-4 | Desativação e remoções acontecem juntas ou nenhuma: tudo no mesmo `SaveChangesAsync`. Se a desativação for recusada (própria conta, RN-17, último administrador, já desativado), nenhum chamado muda. Um conflito de concorrência num chamado devolve 409 e nada é salvo. | pedido; fato do repositório |
| REQ-5 | Reativar o usuário não devolve os chamados. | DEC-4 |
| REQ-6 | A regra vale para qualquer usuário desativado com chamados ativos atribuídos, não só para quem tem a role Atendente (alguém que perdeu `tickets:manage` pode ter ficado com chamados). | DEC-5 |

## 3. Critérios de aceitação
Dados comuns: Ana (Atendente) responsável pelos chamados C1 (Aberto), C2 (Em Andamento), C3 (Resolvido) e C4 (Aberto, excluído); Bruno (Atendente) responsável por C5 (Aberto); o administrador do seed.

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-2 | os dados comuns | o administrador desativa Ana | 204; C1 e C2 com `assignedToUserId` nulo; C1 continua Aberto e C2 Em Andamento; C3 continua com Ana; C4 não é tocado; C5 continua com Bruno |
| AC-2 | REQ-3 | depois do AC-1 | `GET tickets/{C1}/history` e `GET tickets/{C2}/history` | cada um tem uma linha nova com a ação de remoção automática, `changedByUserId` = administrador e a descrição "Responsável removido automaticamente (usuário desativado): Ana …"; C3 não tem linha nova |
| AC-3 | REQ-4 | Ana é a última administradora ativa (cenário do `AdministratorGuard`) ou quem desativa não tem a permissão administrativa dela (RN-17) | tenta desativar Ana | erro de hoje (409/403); C1 e C2 continuam com Ana e sem linha nova no histórico |
| AC-4 | REQ-4 | os dados comuns (teste unitário do handler) | desativa Ana | usuário, convites, sessões, chamados e históricos são gravados numa única chamada de `SaveChangesAsync` |
| AC-5 | REQ-4 | C1 alterado por outra requisição entre a leitura e o `SaveChanges` (teste de integração com duas transações) | desativa Ana | 409; Ana continua ativa e C1/C2 continuam com ela |
| AC-6 | REQ-5 | depois do AC-1 | o administrador reativa Ana | C1 e C2 continuam sem responsável |
| AC-7 | REQ-6 | Carla (Colaboradora, sem `tickets:manage`) ainda responsável por um chamado Aberto atribuído antes de perder a role | desativa Carla | o chamado fica sem responsável, com a linha de histórico |
| AC-8 | REQ-1 | Davi, sem chamados | desativa Davi | 204, comportamento igual ao de hoje |
| AC-9 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | O que acontece com os chamados em aberto de um usuário desativado | (a) continuam atribuídos (hoje; o front já avisa isso); (b) voltam para a fila sem responsável | (b). Com (a), o chamado fica preso a alguém que não pode mais agir nele e só sai dali se alguém perceber e reatribuir à mão, com o SLA correndo; com (b), ele reaparece na fila sem responsável (`unassignedOnly`) e qualquer atendente o assume | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-2 | Como marcar no histórico que a remoção foi automática | (a) ação nova `UnassignedOnUserDeactivation = 14` em `TicketHistoryAction`, com texto próprio; (b) reaproveitar `Unassigned` com um marcador em `NewValue`; (c) reaproveitar `Unassigned` sem distinção | (a): explícito no dado e no texto, sem valor mágico; o front que lista as ações precisa conhecer a nova (ver seção 6) | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-3 | Chamado Em Andamento volta para Aberto? | (a) não, só perde o responsável (como a remoção manual, spec 001); (b) volta para Aberto | (a): mesma regra da remoção manual; mudar status mexeria em cronômetros de SLA e no histórico de status | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-4 | Reativar devolve os chamados? | (a) não; (b) sim, se ainda estiverem sem responsável | (a): quem assumiu nesse meio-tempo não pode perder o chamado, e (b) exigiria guardar estado extra | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-5 | A regra vale só para quem tem a role Atendente? | (a) qualquer usuário com chamados ativos atribuídos; (b) só Atendentes | (a): o que importa é o chamado ter um responsável que não pode mais agir | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-6 | Como remover | (a) carregar os chamados rastreados, alterar e gravar histórico no mesmo `SaveChanges` (passa por auditoria, eventos, interceptor da 015 e concorrência); (b) `ExecuteUpdateAsync` em lote | (a): (b) pularia auditoria, histórico, eventos e o `xmin`; o volume por atendente é pequeno | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |

## 5. Fora do escopo
- Redistribuir os chamados automaticamente para outro atendente.
- Notificar por e-mail os solicitantes ou a equipe.
- Tratar o usuário desativado como solicitante: os chamados que ele **abriu** não mudam.
- Endpoint novo para contar os chamados afetados (o front usa `GET tickets`, ver seção 6).

## 6. Plano técnico
- **Pré-requisito:** spec 015 mergeada (o interceptor de última atividade passa a atualizar os chamados afetados sem código extra); branch a partir da `origin/main` atualizada.
- **Arquivos a criar ou alterar:**
  - Domain:
    - `Tickets/TicketHistoryAction.cs`: `UnassignedOnUserDeactivation = 14` (DEC-2).
  - Application:
    - `TicketHistories/TicketHistoryEntries.cs`: método `UnassignedOnUserDeactivation(ticketId, changedByUserId, oldAssignedToUserId, occurredAtUtc)`, com `OldValue` = id do anterior, como `Unassigned`.
    - `TicketHistories/TicketHistoryDescriptions.cs`: texto "Responsável removido automaticamente (usuário desativado): {oldUserName ?? RemovedUserName}".
    - `TicketHistories/GetAll/GetTicketHistoryQueryHandler.cs:49`: incluir a ação nova na condição que resolve o nome do `OldValue`, junto de `Reassigned` e `Unassigned`.
    - `Users/Deactivate/DeactivateUserCommandHandler.cs`, depois das verificações e antes do `SaveChangesAsync` atual:
      - carregar `context.Tickets.Where(t => t.AssignedToUserId == user.Id && (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress))` (o filtro global já exclui os chamados excluídos);
      - para cada um: `AssignedToUserId = null`; `Raise(new TicketUnassignedDomainEvent(ticket.Id, user.Id, userContext.UserId))`; `context.TicketHistories.Add(TicketHistoryEntries.UnassignedOnUserDeactivation(..., user.DeactivatedAtUtc.Value))`;
      - manter o único `SaveChangesAsync` e a invalidação do cache depois do commit;
      - comentário em pt-BR explicando por que os chamados saem do usuário (a regra da DEC-1).
  - Web.Api: nenhuma mudança na rota (`PUT users/{id}/deactivate`, `users:deactivate`, 204).
  - Testes: `tests/TickestPristine.Application.UnitTests/Users/DeactivateUserCommandHandlerTests.cs` (estender), `tests/TickestPristine.IntegrationTests/Users/UsersTests.cs` (`Deactivate_*`) e o teste das descrições do histórico.
- **Contratos:** a rota não muda. Muda o efeito: chamados ativos perdem o responsável. O histórico ganha a ação 14; exemplo de item em `GET tickets/{id}/history`:

  ```json
  {
    "id": "…",
    "ticketId": "…id do chamado…",
    "action": 14,
    "description": "Responsável removido automaticamente (usuário desativado): Ana Souza",
    "changedByUserId": "…id do administrador…",
    "oldValue": "…id da Ana…",
    "newValue": null,
    "occurredAtUtc": "2026-10-27T21:40:00Z"
  }
  ```

  Formato do `TicketHistoryResponse` atual, sem mudança.
- **Mudança no banco:** não (a ação é um inteiro na coluna existente).
- **Front-end (`tickest-pristine-front`):**
  - modal de desativação de usuário: antes de abrir, consultar `GET tickets?assignedToUserId={id}&status=Open&status=InProgress&pageSize=1` e ler o `totalCount`;
    - com N > 0: "Ao desativar {nome}, {N} chamado(s) em aberto atribuído(s) a ele(a) voltará(ão) para a fila, sem responsável. Cada chamado registrará a remoção no histórico. Reativar o usuário não devolve os chamados.";
    - com N = 0: manter o texto atual de desativação, sem o aviso de chamados;
  - linha do tempo do chamado: tratar a ação 14 (ícone/rótulo de "remoção automática"); como a descrição vem pronta da API, o mínimo é não quebrar com uma ação desconhecida;
  - depois da desativação, recarregar a fila/monitor se estiver aberta.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 2.6 (RN nova da desativação), 2.7 C-05 e C-18, 5.8 e 5.10.
- **Riscos:**
  - R-1: atendente com muitos chamados ativos — a desativação carrega e grava todos numa transação; com dezenas ou poucas centenas, é aceitável.
  - R-2: qualquer alteração simultânea num dos chamados derruba a desativação inteira com 409 (REQ-4); o administrador repete a operação.
  - R-3: o SLA continua correndo sem responsável; a fila/monitor precisa destacar chamados sem responsável (já existe `unassignedOnly` em `GET tickets`).

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Ação de histórico nova, montagem e texto, com a resolução do nome | AC-2 | `add-feature` | [ ] |
| T-2 | Remoção dos responsáveis na desativação, no mesmo `SaveChanges` | AC-1, AC-3 a AC-8 | `add-feature` | [ ] |
| T-3 | Testes unitários e de integração | AC-1 a AC-8 | `add-tests` | [ ] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-9 | — | [ ] |
| T-5 | Revisão de convenções, build sem avisos e suíte inteira | AC-9 | `ca-review` | [ ] |

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

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:**
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir do item 5 do pedido de Rafael Santos no chat (itens 5 e 6 da primeira versão). |
| 2026-10-27 | Em descoberta | Escrita para a opção (b); DEC-1 (decisão de produto) e DEC-2 a DEC-6 aguardam resposta. Se a DEC-1 for (a), a spec passa a `Descartada`. |
| 2026-10-27 | Em descoberta | Rafael Santos delegou as decisões ao agente ("responda vc como senior .net"); DECs fechadas com as recomendações, sem `[SUPOSIÇÃO]` pendente. Aguardando aprovação explícita. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat ("aprovo 017 a 021"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Só começa depois do merge da spec 015 na `main`. |
| 2026-10-27 | Descartada | Descartada por Rafael Santos no chat: conflito com a DEC-4 da spec 014 (chamado `InProgress` volta para `Open`) resolvido a favor da 014 ("Volta para que seja alocado outro responsável"), e o restante desta spec incorporado à 014 ("pode aproveitar a 21 ok"): ação de histórico própria, atomicidade com 409 e regra para qualquer usuário (014 DEC-6 a DEC-9). |
