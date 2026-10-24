# SPEC-01 — Atribuição e reatribuição de responsável em chamados do Backlog

> **Fonte da spec 009.** Especificação de Rafael Santos ("SPEC-01 — Atribuição e Reatribuição de Responsáveis em Tickets do Backlog", recebida duas vezes, idêntica), refinada no chat em 2026-10-24 contra a `origin/main` `5ca764b`. Cópia da aba "SPEC-01" do Claude Doc https://claude.ai/artifact/9JGaAcgbr31TqC7mmWuJx9. O que vale para o desenvolvimento, e com quais decisões, está na spec 009.

Versão 2.0. **O backend de atribuição já existe** (`PUT tickets/{id}/assignee`, com histórico `Reassigned`). Esta spec acrescenta a consulta de atendentes elegíveis, o vínculo do usuário com um setor (hoje inexistente), a restrição da atribuição ao Administrador, o histórico distinguindo atribuição de reatribuição e o modal no front.

## Lacunas e ambiguidades corrigidas

| # | Lacuna ou risco na versão 1.0 | Fato no código | Tratamento nesta versão |
| --- | --- | --- | --- |
| L-01 | "Backlog" usado como estado do chamado | Os estados são Aberto, Em andamento, Resolvido, Cancelado e Fechado; a atribuição já aceita só chamados ativos (Aberto ou Em andamento) | Backlog = chamado ativo (RF-03.2, D-01) |
| L-02 | O DTO exige departamento e setor do atendente | `User` não tem setor nem departamento; só `Sector` tem `DepartmentId` | Novo vínculo usuário → setor (RF-01, D-02) |
| L-03 | Role `Attendant` | A role se chama `Atendente`; a regra atual de elegibilidade é a permissão `tickets:manage`, que o Administrador também tem | Elegível = role Atendente **e** permissão efetiva `tickets:manage` (RF-02.2, D-03) |
| L-04 | "Administrador atribui" | Hoje qualquer usuário com `tickets:manage` atribui, inclusive o Atendente | Nova permissão administrativa `tickets:assign` (RF-03.1, D-04) |
| L-05 | Ordenação por setor feita no front | Com paginação, ordenar no cliente embaralha as páginas | Ordenação no backend por `prioritySectorId` (RF-02.6) |
| L-06 | Consulta sem limite de resultados | Uma busca vazia devolveria todos os usuários | Paginação obrigatória, máximo de 50 por página (RF-02.5) |
| L-07 | Termo de busca sem regras | Curingas `%` e `_` sem escape viram busca total | Escape, tamanho mínimo e máximo (RF-02.4) |
| L-08 | Histórico sem tipo de operação | Só existe a ação `Reassigned`, mesmo na primeira atribuição | Ações `Assigned` e `Reassigned` (RF-04.1) |
| L-09 | Corrida entre dois administradores | `tickets` tem controle por `xmin` | 409 explícito e mensagem no front (RF-03.6, RF-05.7) |
| L-10 | Corpo do 403 indefinido | A policy `HasPermission` responde 403 sem mensagem de negócio | Problem Details com código e mensagem fixa (RF-02.7) |
| L-11 | Atendente sem setor | Situação comum enquanto o vínculo for novo | Continua elegível, exibido como "Sem setor" (RF-02.3) |

## 1. Escopo, atores e convenções

**Dentro do escopo:** vínculo do usuário com um setor; consulta paginada de atendentes elegíveis; atribuição e reatribuição de responsável por quem tem `tickets:assign`; histórico de atribuição; modal de atribuição no front Angular.

**Fora do escopo:** remoção de responsável (já existe, spec 001), notificação ao responsável (não há envio de e-mail no sistema), regra do responsável indicado na abertura do chamado (Q-05), atribuição automática ou por rodízio.

**Convenções:** DEVE / NÃO DEVE = obrigatório; DEVERIA = recomendado, desvio justificado; PODE = opcional. Código e identificadores em inglês; mensagens, comentários e textos de interface em português do Brasil.

| Ator | Role | O que faz nesta spec |
| --- | --- | --- |
| Administrador | Administrador | Consulta atendentes, atribui e reatribui responsáveis, define o setor dos usuários. |
| Atendente | Atendente | É o responsável elegível. Trata os chamados (`tickets:manage`), mas não consulta usuários nem atribui (D-04). |
| Colaborador | Colaborador | Não participa. Vê o responsável do próprio chamado. |
| Sistema | — | Valida elegibilidade, grava histórico e auditoria, controla concorrência. |

**Definições:**
- **Chamado no Backlog:** chamado não excluído com status Aberto ou Em andamento (`TicketStatusTransitions.IsActive`).
- **Atendente elegível:** usuário com `IsActive = true`, com a role Atendente **e** com a permissão efetiva `tickets:manage` (conferida pelo `IPermissionProvider`, que já desconsidera usuários inativos). Ter só a role não basta, porque o Administrador pode tirar `tickets:manage` da role Atendente.
- **Atribuição inicial:** o chamado não tinha responsável. **Reatribuição:** já tinha outro responsável.

## 2. Requisitos funcionais

### RF-01 — Setor do usuário

- RF-01.1: O usuário DEVE poder ter no máximo um setor (`SectorId` opcional em `User`). O departamento é o do setor; o sistema NÃO DEVE gravar o departamento no usuário.
- RF-01.2: O sistema DEVE oferecer `PUT users/{id}/sector` com `{ sectorId }` (nulo remove o vínculo), exigindo `users:update` e respeitando o `PrivilegeEscalationGuard`.
- RF-01.3: O sistema DEVE recusar setor inexistente ou desativado (404 `Sectors.NotFound`).
- RF-01.4: A desativação de um setor NÃO DEVE apagar o vínculo; o setor desativado continua exibido no histórico e nas consultas, marcado como inativo.

### RF-02 — Consulta de atendentes elegíveis (`GetBacklogAttendantsQuery`)

- RF-02.1: O sistema DEVE expor `GET users/attendants`, que exige a permissão `users:read`. A permissão `tickets:manage` NÃO DEVE substituí-la.
- RF-02.2: A consulta DEVE devolver somente atendentes elegíveis (seção 1). Usuários inativos, sem a role Atendente ou sem `tickets:manage` efetiva NÃO DEVEM aparecer, em nenhuma combinação de filtros.
- RF-02.3: Cada item (`AttendantLookupDto`) DEVE conter `Id`, `FullName` (nome e sobrenome), `Email`, `SectorId`, `SectorName` e `DepartmentName`. Sem setor, os três últimos vêm nulos e o front exibe "Sem setor".
- RF-02.4: O parâmetro `searchTerm` é opcional. Quando informado, DEVE ter de 2 a 100 caracteres após `Trim`, ser comparado sem diferenciar maiúsculas e minúsculas por "contém" em nome completo, e-mail, nome do setor e nome do departamento, e ter `%`, `_` e `\` escapados. Fora do tamanho, a resposta DEVE ser 400 de validação.
- RF-02.5: A resposta DEVE ser paginada no padrão existente (`Abstractions/Pagination`): `page` a partir de 1, `pageSize` padrão 20 e máximo 50.
- RF-02.6: O filtro `sectorId` é opcional e restringe ao setor. O parâmetro `prioritySectorId` é opcional e só ordena: atendentes desse setor primeiro, depois os demais; dentro de cada grupo, por `FullName` crescente e, no empate, por `Id`. Todos os filtros DEVEM ser aplicados ao mesmo tempo (E lógico).
- RF-02.7: Sem `users:read`, a resposta DEVE ser 403 em Problem Details, com `code` = `Users.ReadForbidden` e `detail` = "Você não tem permissão para buscar usuários.". O sistema NÃO PODE devolver 200 com lista vazia nesse caso.
- RF-02.8: Resultado vazio por filtro DEVE ser 200 com `items` vazio e `totalCount` = 0.

### RF-03 — Atribuição e reatribuição (`AssignTicketCommand`, rota existente)

- RF-03.1: `PUT tickets/{id}/assignee` DEVE passar a exigir a nova permissão administrativa `tickets:assign`, concedida só à role Administrador (D-04). A permissão `tickets:assign` NÃO DEVE conceder `users:read`, e vice-versa.
- RF-03.2: O sistema DEVE validar, nesta ordem, devolvendo o primeiro erro encontrado: (1) id do chamado e do responsável não vazios — 400; (2) chamado existe e não está excluído — 404 `Tickets.NotFound`; (3) chamado no Backlog — 400 `Tickets.NotActive`; (4) responsável existe e está ativo — 404 `Users.NotFound`; (5) responsável é atendente elegível — 400 `Tickets.AssigneeNotAttendant`.
- RF-03.3: Atribuir ao responsável atual DEVE responder 204 sem gravar nada (comportamento atual mantido).
- RF-03.4: O sistema NÃO DEVE restringir a atribuição ao setor do chamado; a priorização por setor é só de exibição (D-05).
- RF-03.5: A troca do responsável e o registro de histórico DEVEM ser gravados na mesma transação (`SaveChangesAsync` único).
- RF-03.6: Duas atribuições concorrentes no mesmo chamado DEVEM resultar em uma gravada e a outra em 409, pelo controle de concorrência `xmin` já existente.
- RF-03.7: O responsável anterior perde o acesso ao chamado, salvo se for o solicitante ou tiver `tickets:manage` (RN-23, comportamento atual).

### RF-04 — Histórico

- RF-04.1: Toda atribuição com efeito DEVE gravar em `ticket_histories` uma entrada com a ação `Assigned` (sem responsável anterior) ou `Reassigned` (com anterior), o id do chamado, o id do responsável anterior (`oldValue`, nulo na atribuição inicial), o id do novo (`newValue`), o id do executor e a data e hora em UTC.
- RF-04.2: A ação `Assigned` DEVE ser gravada como texto, como as demais (sem migration de enum).
- RF-04.3: A descrição exibida no histórico DEVE mostrar os nomes do responsável anterior e do novo, resolvidos na leitura; usuário desativado continua com o nome exibido.
- RF-04.4: O histórico é imutável: NÃO DEVE existir rota de edição ou exclusão de entradas.

### RF-05 — Modal de atribuição (`AssignResponsibleModalComponent`, Angular)

- RF-05.1: O modal DEVE usar um autocomplete pesquisável, com espera de 300 ms após a digitação antes de consultar e cancelamento da requisição anterior ainda pendente.
- RF-05.2: Ao abrir, o modal DEVE carregar a primeira página com `prioritySectorId` = setor do chamado e sem `searchTerm`.
- RF-05.3: Cada opção DEVE mostrar, na linha 1, `FullName` em destaque; na linha 2, em texto secundário, `email • Departamento / Setor`, ou `email • Sem setor`.
- RF-05.4: O modal DEVE separar visualmente o grupo "Mesmo setor do chamado" do grupo "Outros setores".
- RF-05.5: Durante a consulta, DEVE exibir indicador de carregamento. Durante a atribuição, DEVE desabilitar o botão de confirmar e o campo, e NÃO DEVE permitir um segundo envio.
- RF-05.6: As respostas DEVEM gerar mensagens distintas: 403 na consulta → "Você não tem permissão para buscar usuários." (sem exibir lista); 200 vazio → "Nenhum atendente encontrado para a busca."; falha de rede ou 5xx → "Não foi possível carregar os atendentes. Tente novamente." com botão de tentar de novo.
- RF-05.7: Na atribuição: 204 → fecha o modal, atualiza o chamado e o histórico na tela; 409 → "O chamado foi alterado por outra pessoa. Recarregue e tente novamente."; 400 ou 404 → a mensagem `detail` do Problem Details; 403 → "Você não tem permissão para atribuir chamados.".
- RF-05.8: O botão de abrir o modal DEVERIA ficar oculto para quem não tem `tickets:assign`; a interface NÃO DEVE ser o único controle.

## 3. Matriz de permissões

Toda verificação DEVE ocorrer no backend. Esconder um botão não é controle de acesso.

| Operação | Permissão exigida | Administrador | Atendente | Colaborador |
| --- | --- | --- | --- | --- |
| Consultar atendentes (`GET users/attendants`) | `users:read` | Sim | Não (403) | Não (403) |
| Atribuir ou reatribuir (`PUT tickets/{id}/assignee`) | `tickets:assign` (nova) | Sim | Não (403), D-04 | Não (403) |
| Remover responsável (`DELETE tickets/{id}/assignee`) | `tickets:manage` (inalterado) | Sim | Sim | Não |
| Definir setor de um usuário (`PUT users/{id}/sector`) | `users:update` | Sim | Não | Não |
| Ver histórico do chamado | Participante ou `tickets:manage` (inalterado) | Sim | Sim | Só os próprios |

Regras de dados:
- `tickets:assign` DEVE ser registrada no `PermissionCatalog` como administrativa: só quem a possui a concede ou remove (`PrivilegeEscalationGuard`).
- Entradas de histórico e de auditoria são imutáveis.
- O DTO da consulta NÃO DEVE expor dados além dos seis campos de RF-02.3 (minimização, LGPD art. 6º, III).

## 4. Contratos da API

| Rota | Entrada | Sucesso | Erros |
| --- | --- | --- | --- |
| `GET users/attendants` | Query: `searchTerm?`, `sectorId?`, `prioritySectorId?`, `page`, `pageSize` | 200, página de `AttendantLookupDto` | 400 validação · 401 · 403 `Users.ReadForbidden` · 429 |
| `PUT tickets/{id}/assignee` | `{ assignedToUserId }` | 204 | 400 (`Validation`, `Tickets.NotActive`, `Tickets.AssigneeNotAttendant`) · 401 · 403 · 404 (`Tickets.NotFound`, `Users.NotFound`) · 409 |
| `PUT users/{id}/sector` | `{ sectorId \| null }` | 204 | 400 · 401 · 403 · 404 (`Users.NotFound`, `Sectors.NotFound`) · 409 |

Todo erro DEVE seguir o Problem Details já usado (`CustomResults.Problem`), sem stack trace nem nome de tabela.

## 5. Requisitos não funcionais

- RNF-01 Auditoria: a troca de responsável e de setor DEVE passar pelo `AuditLoggingInterceptor` existente; toda resposta 403 dessas rotas DEVE gerar log estruturado de aviso com usuário, rota e permissão ausente.
- RNF-02 Segurança: busca com parâmetros do EF Core (sem SQL concatenado); escape de curingas (RF-02.4); verificação de acesso por recurso (IDOR) mantida no `TicketAccessGuard`.
- RNF-03 Rate limit: `GET users/attendants` DEVE ter limite próprio de 60 requisições por minuto por usuário, além do global (100/60 s).
- RNF-04 Idempotência: `PUT tickets/{id}/assignee` é idempotente por natureza (RF-03.3); o front DEVE impedir envio duplo (RF-05.5).
- RNF-05 Desempenho: a consulta DEVE responder em até 300 ms no p95 com 5.000 usuários; o índice em `users.sector_id` DEVE existir.
- RNF-06 Testes: unitários dos handlers e validators e de integração por HTTP para cada linha da matriz da seção 3, autorizada e negada.

## 6. Critérios de aceitação

Ver a spec 009, seção 3 (os cenários CA-01 a CA-11 do documento refinado foram convertidos em AC verificáveis lá).

## 7. Decisões pendentes

| ID | Questão | Opção adotada nesta versão | Alternativa |
| --- | --- | --- | --- |
| D-01 | Backlog inclui chamados Em andamento? | Sim: todo chamado ativo, como a regra atual | Só Aberto |
| D-02 | Usuário pode atender mais de um setor? | Não: um setor por usuário (`users.sector_id`) | Tabela N:N usuário × setor |
| D-03 | O Administrador pode ser responsável? | Não: só a role Atendente (muda a regra atual, que aceita qualquer `tickets:manage`) | Manter `tickets:manage` como único critério |
| D-04 | O Atendente deixa de poder assumir chamados sozinho? | Sim: atribuir passa a exigir `tickets:assign` | Atendente pode atribuir só a si mesmo, sem `tickets:assign` |
| D-05 | A atribuição fica restrita ao setor do chamado? | Não: o setor só ordena a lista | Bloquear atendentes de outro setor |
