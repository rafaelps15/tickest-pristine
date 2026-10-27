---
id: 015
titulo: "Meus Chamados" com responsável, setor, prazo, última atividade, filtro e busca
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-015-own-tickets-listing                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr: "#32"                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 015 — "Meus Chamados" com responsável, setor, prazo, última atividade, filtro e busca

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat da sessão do front-end (`tickest-pristine-front`), em 2026-10-27: "gere a próxima especificação técnica para destravar o back-end" da tela "Meus Chamados", com filtro "Em aberto / Encerrados / Todos", busca, paginação e, por chamado, setor, responsável, prazo/SLA e última atividade ("atualizado há X tempo"). O pedido colado pedia blocos C# prontos; na descoberta, o responsável escolheu seguir esta skill, sem código (DEC-1).
- **Origem do requisito:** brief da tela "Meus Chamados" aprovado no chat do front em 2026-10-27 (critique da tela: 25/40; os dois P1 dependem desta spec). As regras de cancelar em 15 minutos, excluir só com permissão e reabrir com motivo ficam para a spec 016 (DEC-4).
- **Referências:**
  - `SYSTEM_SPECIFICATION.md` 4.3 (rotas; `GET tickets/mine` com `tickets:view-own`, linha 521) e 2.6 (regras de negócio de chamados).
  - Spec 006 (situação do SLA, `TicketSla.GetStatus`) e spec 002 (histórico do chamado).
- **Fatos do repositório:**
  - `GET tickets/mine` (`Web.Api/Endpoints/Tickets/GetOwn.cs`) recebe só a paginação (`PageRequest`) e exige `tickets:view-own`. A consulta (`Tickets/GetOwn/GetOwnTicketsQuery.cs`) pega o usuário do token e ordena por `CreatedAtUtc` e `Id`, decrescentes (`GetOwnTicketsQueryHandler.cs:16-18`).
  - `OwnTicketResponse` traz id, título, descrição, prioridade, status, `OpenedByUserId`, `AssignedToUserId` e `CreatedAtUtc`. Não traz nome do responsável, setor, prazo, situação do SLA nem atividade.
  - O `GetTicketsQueryHandler` (listagem geral, usada pelo Monitor) já monta `AssignedToName` e `SectorName` por subconsulta no `.Select` e calcula `SlaStatus` com `TicketSla.GetStatus` na projeção (`Tickets/GetAll/GetTicketsQueryHandler.cs:52-72`). A busca dele é `Title.Contains`, que no PostgreSQL diferencia maiúsculas e minúsculas.
  - A entidade `Ticket` (`Domain/Tickets/Ticket.cs`) não tem data de última atividade. Toda ação sobre o chamado grava uma linha de `TicketHistory` (13 ações em `TicketHistoryAction`, inclusive mensagens e anexos), montada por `TicketHistoryEntries` em 12 handlers e no `SampleDataSeeder`, sempre no mesmo `SaveChangesAsync` da mudança.
  - O projeto já tem um interceptor de `SaveChanges`, o `AuditLoggingInterceptor`, registrado em `Infrastructure/DependencyInjection.cs:118,124`.
  - `TicketConfiguration.cs:51-52` tem o índice `(CreatedByUserId, CreatedAtUtc desc, Id desc)`, que só serve a ordenação atual de "Meus chamados". `TicketConfiguration.cs:41` define `Version` como versão da linha (concorrência otimista).
  - Banco: PostgreSQL 17 (Npgsql). As datas são `DateTime` em UTC, com sufixo `Utc`.
  - O chamado não tem número legível, só o `Guid`; por isso a busca é só pelo título.
  - "Em aberto" no domínio é `Open` e `InProgress`; os demais status são `Resolved`, `Closed` e `Canceled` (`TicketStatus.cs`).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Cada item de `GET tickets/mine` passa a trazer também: `assignedToName` (nome completo do responsável, ou nulo sem responsável), `sectorId` e `sectorName` (setor de destino), `dueAtUtc` (prazo atual), `slaStatus` (mesma situação e mesma regra do `GET tickets`) e `lastActivityAtUtc`. Os campos atuais continuam iguais. | pedido; DEC-3 |
| REQ-2 | `lastActivityAtUtc` é a data da ação mais recente registrada no histórico do chamado, qualquer que seja a ação; um chamado recém-aberto tem a data da abertura. | pedido; DEC-2; DEC-5 |
| REQ-3 | `GET tickets/mine` aceita o filtro opcional `status`, um enum lido como os demais enums das rotas de chamados (`slaStatus` e `sort` do `GET tickets`): `Active` traz só `Open` e `InProgress`; `Finished` traz só `Resolved`, `Closed` e `Canceled`; sem o parâmetro, traz todos, como hoje. Um valor que não seja do enum responde 400. | pedido; DEC-7 |
| REQ-4 | `GET tickets/mine` aceita a busca opcional `search`, sem diferenciar maiúsculas e minúsculas, pelo trecho contido no título. Espaços nas pontas são ignorados; busca vazia ou só com espaços equivale a sem busca. | pedido; brief da tela |
| REQ-5 | A lista vem ordenada pela última atividade, da mais recente para a mais antiga, com o id para desempatar. Filtro e busca se combinam com a paginação atual, e `totalCount` reflete o que foi filtrado. | pedido; DEC-8 |
| REQ-6 | Continua valendo: só os chamados abertos pelo usuário do token, com `tickets:view-own`. | fato do repositório |
| REQ-7 | Os chamados que já existem ganham `lastActivityAtUtc` igual à data da ação mais recente do histórico deles, ou à data de abertura quando não houver histórico. | DEC-9 |

## 3. Critérios de aceitação
Dados comuns: setor Helpdesk; Maria (solicitante, com `tickets:view-own`); Ana (atendente); Paulo (outro solicitante).

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | um chamado de Maria no Helpdesk, atribuído a Ana, e outro sem responsável | Maria chama `GET tickets/mine` | 200; o primeiro traz `assignedToName` = nome completo de Ana, `sectorName` = "Helpdesk", `sectorId`, `dueAtUtc` e `slaStatus` iguais aos do mesmo chamado em `GET tickets`; o segundo traz `assignedToName` nulo; os campos de antes continuam presentes e iguais |
| AC-2 | REQ-2 | um chamado de Maria aberto às 9h | Ana publica uma mensagem às 10h; Maria chama `GET tickets/mine` | `lastActivityAtUtc` = 10h |
| AC-3 | REQ-2 | um chamado de Maria com atividade às 10h | Ana troca o status às 11h, depois anexa um arquivo às 12h; Maria consulta depois de cada ação | `lastActivityAtUtc` = 11h e depois 12h; nunca volta para uma data anterior |
| AC-4 | REQ-2 | Maria abre um chamado agora | Maria chama `GET tickets/mine` | `lastActivityAtUtc` = `createdAtUtc` do chamado |
| AC-5 | REQ-3 | Maria com chamados Aberto, Em Andamento, Resolvido, Fechado e Cancelado | chama com `status=Active`, com `status=Finished`, sem `status` e com `status=xyz` | `Active`: só Aberto e Em Andamento; `Finished`: só os outros três; sem o parâmetro: os cinco; `xyz`: 400 |
| AC-6 | REQ-4 | chamados de Maria "Impressora não imprime" e "Acesso ao e-mail" | chama com `search=IMPRESS`, com `search=  e-mail  ` e com `search=   ` | só o primeiro; só o segundo; os dois |
| AC-7 | REQ-5 | três chamados de Maria abertos em ordem A, B, C, com uma mensagem nova em A depois de C | chama `GET tickets/mine` com `pageSize=2` e `status=Active` | página 1 com A e C, nessa ordem; `totalCount` = 3; a página 2 traz B |
| AC-8 | REQ-6 | um chamado de Paulo com o mesmo título de um de Maria | Maria busca pelo título; um usuário sem `tickets:view-own` chama a rota | só o chamado de Maria; 403 para o usuário sem a permissão; 401 sem token |
| AC-9 | REQ-7 | um banco com chamados antigos, um deles com histórico e outro sem nenhuma linha de histórico | aplica a migration | o primeiro fica com a maior data do histórico dele; o segundo, com a data de abertura |
| AC-10 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Formato da spec | (a) seguir a `spec-new`, sem código; (b) blocos C# prontos para commit, como no pedido colado; (c) só preparar o texto para rodar a `spec-new` numa sessão do back-end | (a): o código entra pela `spec-implement`, com o portão SDD | Rafael Santos, 2026-10-27, no chat |
| DEC-2 | Como obter a última atividade | (a) coluna nova no `Ticket`, mantida a cada ação, com migration e índice; (b) calcular na consulta pela maior data do histórico | (a): ordenar e paginar por ela fica barato | Rafael Santos, 2026-10-27, no chat |
| DEC-3 | Endpoint | (a) estender `GET tickets/mine`, com campos e filtros aditivos; (b) endpoint novo | (a): mesmo endpoint e mesma permissão; o front já consome; nada quebra | Rafael Santos, 2026-10-27, no chat |
| DEC-4 | As regras de cancelar em 15 min, excluir só com permissão e reabrir com motivo entram aqui? | (a) spec separada; (b) tudo nesta spec | (a): esta spec só lê; as regras de escrita vão para a spec 016 (a 014 já estava ocupada por outra spec) | Rafael Santos, 2026-10-27, no chat |
| DEC-5 | O que conta como atividade | (a) toda linha do histórico (as 13 ações); (b) só o que importa ao solicitante | (a): regra simples, igual ao que o Histórico do detalhe mostra | Rafael Santos, 2026-10-27, no chat |
| DEC-6 | Como manter a coluna atualizada | (a) interceptor do `SaveChanges`: a cada `TicketHistory` novo, atualiza o chamado no mesmo `SaveChanges`; (b) cada um dos 12 handlers atualiza o chamado | (a): um lugar só, impossível um handler novo esquecer; precedente no `AuditLoggingInterceptor` | Rafael Santos, 2026-10-27, no chat |
| DEC-7 | Nomes e padrão do filtro | (a) `active` / `finished`, sem filtro = todos; (b) `open` / `closed` / `all` | (a): `closed` confundiria com o status `Closed`; sem o parâmetro, a rota se comporta como hoje. Os valores são os do enum `OwnTicketStatusGroup` (`Active`, `Finished`), lidos como os demais enums das rotas | Rafael Santos, 2026-10-27, no chat |
| DEC-8 | Ordenação | (a) última atividade, mais recente primeiro; (b) abertura, como hoje | (a): o que acabou de mudar é o que o solicitante procura | Rafael Santos, 2026-10-27, no chat |
| DEC-9 | Preenchimento dos chamados existentes | (a) maior data do histórico, ou a abertura sem histórico; (b) data de abertura | (a): fica igual ao que o interceptor teria gravado | Rafael Santos, 2026-10-27, no chat |
| DEC-10 | Base da branch | (a) `origin/main` atualizada; (b) depois de outra spec | (a): segue o `CLAUDE.md`; não depende das specs 011, 012 e 014 em andamento | Rafael Santos, 2026-10-27, no chat |
| DEC-11 | Na implementação da T-2, o interceptor da DEC-6 fez cada mensagem alterar a linha do chamado, protegida pelo `xmin`: duas mensagens simultâneas passaram a conflitar, e o teste da RN-26 (`TicketsTests.PostMessage_Should_RecordBothMessagesAndSingleCompletion_WhenTwoAgentsAnswerAtOnce`) falhou. Como manter a última atividade sem quebrar a RN-26? | (a) chamado criado ou alterado no mesmo `SaveChanges`: a data vai na própria entidade; os demais (mensagem, anexo, chamado só lido): `UPDATE` direto depois do commit, com `GREATEST` e sem checagem de versão; (b) tabela própria de atividade, gravada por upsert; (c) manter o plano e fazer o envio de mensagem regravar no conflito | (a). Ajusta a DEC-6: nesses casos, a atualização sai da transação da ação; se o processo cair entre o commit e o `UPDATE`, a data fica para trás até a próxima ação (dado derivado) | Rafael Santos, 2026-10-27, no chat ("a") |

## 5. Fora do escopo
- Janela de 15 minutos para o solicitante cancelar, exclusão só com permissão e reabertura com motivo (spec 016, DEC-4).
- Indicador de "nova resposta não lida" (decidido fora no brief da tela: só a última atividade).
- Busca por outro campo além do título; ordenação escolhida pelo usuário.
- Qualquer mudança em `GET tickets` (Monitor), em `GET tickets/mine/summary` ou no detalhe do chamado.
- O consumo dos campos novos na tela "Meus Chamados" (repositório `tickest-pristine-front`).

## 6. Plano técnico
- **Pré-requisito:** nenhum; branch a partir da `origin/main` (DEC-10).
- **Arquivos a criar ou alterar:**
  - Domain:
    - `Tickets/Ticket.cs`: propriedade `LastActivityAtUtc` (`DateTime`), preenchida na abertura com a mesma data de `CreatedAtUtc`.
  - Application:
    - `Tickets/GetOwn/GetOwnTicketsQuery.cs`: ganha o grupo de status opcional e a busca opcional, além da paginação.
    - `Tickets/GetOwn/OwnTicketStatusGroup.cs` (novo): enum com `Active` e `Finished`.
    - `Tickets/GetOwn/OwnTicketResponse.cs`: `AssignedToName`, `SectorId`, `SectorName`, `DueAtUtc`, `SlaStatus` e `LastActivityAtUtc`.
    - `Tickets/GetOwn/GetOwnTicketsQueryHandler.cs`: filtra pelo grupo, busca pelo título sem diferenciar maiúsculas (`ILIKE` do Npgsql, com o termo escapado), ordena por `LastActivityAtUtc` e `Id` decrescentes e projeta os campos novos com as mesmas subconsultas e o mesmo `TicketSla.GetStatus` do `GetTicketsQueryHandler`, num único comando SQL. A data atual vem do `IDateTimeProvider`.
  - Infrastructure:
    - `Tickets/TicketActivityInterceptor.cs` (novo): antes de salvar, para cada `TicketHistory` em estado `Added`, leva o `LastActivityAtUtc` do chamado ao maior valor entre o atual e o `OccurredAtUtc` da linha, inclusive quando o chamado é criado no mesmo `SaveChanges` (abertura e seed). Nunca diminui a data. **[DEC-11]** Isso vale para o chamado criado ou alterado no mesmo `SaveChanges`; para os demais, o interceptor roda, depois do commit, `UPDATE ... SET last_activity_at_utc = GREATEST(last_activity_at_utc, <data>)`, sem checagem de versão.
    - `DependencyInjection.cs`: registra o interceptor junto do `AuditLoggingInterceptor`.
    - `Tickets/TicketConfiguration.cs`: coluna `last_activity_at_utc` obrigatória; troca o índice `(CreatedByUserId, CreatedAtUtc desc, Id desc)` por `(CreatedByUserId, LastActivityAtUtc desc, Id desc)`.
    - Migration nova: cria a coluna, preenche cada chamado com a maior `occurred_at_utc` do histórico dele ou com `created_at_utc` (DEC-9), e troca o índice.
  - Web.Api:
    - `Endpoints/Tickets/GetOwn.cs`: lê `status` e `search` da query string, além da paginação. Um `status` que não seja do enum dá 400 pela própria leitura do parâmetro, como `slaStatus` e `sort` em `Endpoints/Tickets/GetAll.cs`.
  - Testes:
    - unitários: `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` (estender);
    - integração: `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` (casos `GetOwn_*`) e um arquivo novo para o interceptor e a migration.
- **Contratos:**

  | Rota | Autorização (sem mudança) | Entrada | Resposta | Status |
  |---|---|---|---|---|
  | `GET tickets/mine` | `tickets:view-own` | paginação de sempre; `status` opcional (`Active`, `Finished`); `search` opcional | `PagedResponse<OwnTicketResponse>`, cada item com `assignedToName`, `sectorId`, `sectorName`, `dueAtUtc`, `slaStatus` e `lastActivityAtUtc` além dos campos atuais | 200 · 400 (status inválido) · 401 · 403 |

  Mudança só aditiva: nenhum campo sai nem muda de tipo, e sem os parâmetros novos o filtro é o de hoje. Muda a ordem padrão (DEC-8), que o front atual não assume. Nenhuma rota, erro de domínio ou evento novo.
- **Mudança no banco:** sim. Coluna `tickets.last_activity_at_utc` (não nula), preenchimento dos registros existentes e troca de um índice.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 (parâmetros de `GET tickets/mine`), 2.6 (regra nova da última atividade, como RN) e a seção do modelo de dados de `Ticket`.
- **Análise:**
  - **Concorrência:** a leitura é um único comando SQL, então cada item é consistente. Na escrita, o `Ticket` usa versão de linha (`TicketConfiguration.cs:41`). Com a coluna nova, publicar, editar ou excluir mensagem e anexar ou remover arquivo passam a atualizar a linha do chamado, o que hoje não acontece. Duas ações quase simultâneas no mesmo chamado (por exemplo, uma mensagem e uma troca de status) podem então cair no conflito de concorrência que já existe, e a segunda recebe o erro de conflito, como hoje entre duas trocas de status. Ver R-1.
  - **Quebra de contrato:** nenhuma. Os campos são novos, e a ordem nova não quebra o front atual, que não depende dela.
  - **Desempenho:** a listagem filtra pelo solicitante e ordena pela última atividade, o que o índice novo atende. O filtro por grupo de status e a busca no título rodam sobre os chamados de uma só pessoa; um índice por status, setor ou responsável não ajudaria, porque setor e responsável só são projetados, não filtrados. A busca `ILIKE '%termo%'` não usa índice, o que é aceitável nesse volume (R-2). As subconsultas de responsável e setor vão pelas chaves primárias, sobre no máximo 100 linhas por página.
  - **Segurança:** o usuário continua vindo do token; nenhum chamado de outra pessoa fica visível. O termo da busca é escapado para que `%` e `_` sejam tratados como texto.
- **Árvore de arquivos:**

  ```text
  src/TickestPristine.Domain/Tickets/
    Ticket.cs                              [MODIFIED]  + LastActivityAtUtc
  src/TickestPristine.Application/Tickets/GetOwn/
    GetOwnTicketsQuery.cs                  [MODIFIED]  + grupo de status, busca
    OwnTicketStatusGroup.cs                [NEW]       Active, Finished
    OwnTicketResponse.cs                   [MODIFIED]  + 6 campos
    GetOwnTicketsQueryHandler.cs           [MODIFIED]  filtro, busca, ordem, projeção
  src/TickestPristine.Infrastructure/
    Tickets/TicketActivityInterceptor.cs   [NEW]
    Tickets/TicketConfiguration.cs         [MODIFIED]  coluna e índice
    DependencyInjection.cs                 [MODIFIED]  registro do interceptor
    Database/Migrations/<data>_TicketLastActivity.cs  [NEW]
  src/TickestPristine.Web.Api/Endpoints/Tickets/
    GetOwn.cs                              [MODIFIED]  status e search
  tests/TickestPristine.Application.UnitTests/Tickets/
    GetOwnTicketsQueryHandlerTests.cs      [MODIFIED]
  tests/TickestPristine.IntegrationTests/Tickets/
    TicketsTests.cs                        [MODIFIED]  GetOwn_*
    TicketActivityTests.cs                 [NEW]       interceptor e migration
  ```

- **Testes previstos** (nomes no padrão dos testes existentes; podem ser ajustados na implementação, e a rastreabilidade registra os reais):

  | AC | Unitários (`GetOwnTicketsQueryHandlerTests`) | Integração |
  |---|---|---|
  | AC-1 | `Handle_Should_ReturnAssigneeSectorDueAndSla` | `TicketsTests` › `GetOwn_Should_ReturnAssigneeSectorDueAndSlaFields` |
  | AC-2, AC-3 | — | `TicketActivityTests` › `LastActivity_Should_FollowLatestHistoryEntry`, `LastActivity_Should_NeverMoveBackwards` |
  | AC-4 | — | `TicketActivityTests` › `LastActivity_Should_EqualCreatedAt_WhenTicketIsNew` |
  | AC-5 | `Handle_Should_FilterByStatusGroup` | `TicketsTests` › `GetOwn_Should_FilterByStatusGroup`, `GetOwn_Should_ReturnBadRequest_WhenStatusIsInvalid` |
  | AC-6 | `Handle_Should_SearchTitleIgnoringCase`, `Handle_Should_IgnoreBlankSearch` | `TicketsTests` › `GetOwn_Should_SearchTitleIgnoringCase` |
  | AC-7 | `Handle_Should_OrderByLastActivityDescending` | `TicketsTests` › `GetOwn_Should_PageFilteredResultsByLastActivity` |
  | AC-8 | — | os existentes `GetOwn_Should_NotReturnOtherUsersTickets` e `GetOwn_Should_ReturnUnauthorized_WhenNoTokenProvided`, mais `GetOwn_Should_ReturnForbidden_WithoutViewOwnPermission` |
  | AC-9 | — | `TicketActivityTests` › `Migration_Should_BackfillLastActivityFromHistory` |
  | AC-10 | build e suíte inteira | build e suíte inteira |
- **Riscos:**
  - R-1: mais conflitos de concorrência entre ações quase simultâneas no mesmo chamado, porque mensagens e anexos passam a atualizar a linha do chamado. A janela é de milissegundos e a resposta de conflito já existe e é tratada pelo front ("tente novamente"). Se aparecer na prática, a alternativa é guardar a atividade numa tabela própria, numa spec futura.
  - R-2: a busca `ILIKE '%termo%'` lê todos os chamados do solicitante. Com poucas centenas por pessoa, o custo é baixo; um índice trigram só se justifica se o volume por pessoa crescer muito.
  - R-3: o seed usa datas no passado; o interceptor usa a data da linha do histórico, não a hora atual, para os chamados de exemplo ficarem coerentes.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Coluna `LastActivityAtUtc` no `Ticket`, configuração, troca do índice e migration com o preenchimento dos chamados existentes | AC-4, AC-9 | `add-entity` | [x] |
| T-2 | Interceptor que atualiza a última atividade a cada linha nova de histórico, com o registro na injeção de dependências | AC-2, AC-3, AC-4 | `add-feature` | [x] |
| T-3 | Filtro por grupo de status, busca, ordem e campos novos na consulta, na resposta e no endpoint de `GET tickets/mine` | AC-1, AC-5, AC-6, AC-7, AC-8 | `add-feature` | [x] |
| T-4 | Testes unitários e de integração da seção 6 | AC-1 a AC-9 | `add-tests` | [x] |
| T-5 | Atualizar o `SYSTEM_SPECIFICATION.md` (4.3, regra nova em 2.6 e modelo de dados de `Ticket`) | AC-10 | — | [x] |
| T-6 | Revisão de convenções, build sem avisos e suíte inteira | AC-10 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_ReturnAssigneeSectorDueAndSlaFields_LikeGetAll`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_ReturnAssigneeSectorDueAndSla_WhenTicketIsAssigned` | Pela API, o chamado de Maria atribuído ao administrador traz `assignedToName` = "Admin Master", `sectorId` e `sectorName` do setor, e `dueAtUtc` e `slaStatus` iguais aos do mesmo chamado em `GET tickets`; os campos de antes (título, descrição, prioridade, solicitante) continuam iguais; o chamado sem responsável traz `assignedToName` nulo. O unitário confere o `SlaStatus` contra o `TicketSla.GetStatus` com a data do `IDateTimeProvider`. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketActivityTests.cs` › `LastActivity_Should_FollowLatestHistoryEntry_WhenAgentPostsMessage` | Depois da mensagem de quem atende, `lastActivityAtUtc` em `GET tickets/mine` é igual à data do registro `MessageAdded` do histórico e maior que a abertura (o horário real substitui as 9h e 10h do critério). | Aprovado — 2026-10-27, execução da spec-verify |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketActivityTests.cs` › `LastActivity_Should_AdvanceWithEachAction_WhenStatusChangesAndFileIsAttached`, `LastActivity_Should_NeverMoveBackwards_WhenOlderHistoryEntryIsSaved` | Depois da troca de status e depois do anexo, `lastActivityAtUtc` é igual à data do registro `StatusChanged` e, depois, à do `AttachmentAdded`, sempre maior que a anterior; um registro de histórico com data anterior não faz a data voltar. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketActivityTests.cs` › `LastActivity_Should_EqualCreatedAt_WhenTicketIsNew` | Um chamado recém-aberto pela API traz `lastActivityAtUtc` igual ao `createdAtUtc`. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_FilterByStatusGroup_WhenStatusIsGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_FilterByStatusGroup_WhenGroupIsGiven` | Com um chamado de Maria em cada status: `status=Active` traz só Aberto e Em Andamento; `status=Finished`, só Resolvido, Fechado e Cancelado; sem o parâmetro, os cinco; `status=xyz` responde 400. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_SearchTitleIgnoringCase_WhenSearchIsGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_SearchTitleIgnoringSurroundingSpaces_WhenSearchIsGiven`, `Handle_Should_IgnoreSearch_WhenSearchIsBlank` | Contra o PostgreSQL: `search=IMPRESS` traz só "Impressora não imprime"; `search=  e-mail  ` traz só "Acesso ao e-mail"; `search=   ` traz os dois. O unitário confere o trecho no título, os espaços nas pontas e a busca em branco (maiúsculas dependem do `citext`, conferidas só na integração). | Aprovado — 2026-10-27, execução da spec-verify |
| AC-7 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_PageFilteredResultsByLastActivity_WhenTicketGetsNewMessage`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_OrderByLastActivityDescending_WhenActivityDiffersFromCreation` | A, B e C abertos nessa ordem e uma mensagem nova em A: com `status=Active&pageSize=2`, a página 1 traz A e C, nessa ordem, com `totalCount` = 3, e a página 2 traz B. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-8 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_ReturnOnlyCallerTickets_WhenOtherUserHasTicketWithSameTitle`, `GetOwn_Should_ReturnForbidden_WithoutViewOwnPermission`, `GetOwn_Should_NotReturnOtherUsersTickets`, `GetOwn_Should_ReturnUnauthorized_WhenNoTokenProvided` | Com chamados de Paulo e de Maria de mesmo título, a busca de Maria traz só o dela; um usuário sem nenhuma role (sem `tickets:view-own`) recebe 403; sem token, 401. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-9 | `tests/TickestPristine.IntegrationTests/Tickets/TicketActivityTests.cs` › `Migration_Should_BackfillLastActivityFromHistory_WhenApplied` | Com um chamado com histórico e outro sem histórico, ambos com a coluna zerada, a migration `Add_Ticket_Last_Activity` é desfeita e aplicada de novo pelo `IMigrator`: o primeiro fica com a maior data do histórico e o segundo com a data de abertura. | Aprovado — 2026-10-27, execução da spec-verify |
| AC-10 | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | Build sem incremental com 0 avisos e 0 erros; 1.027 testes aprovados (50 de arquitetura, 616 unitários e 361 de integração) na execução da T-6 e de novo na da `spec-verify`. | Aprovado — 2026-10-27: build 0 avisos e 0 erros; 1.027 aprovados, 0 com falha |

## 9. Evidências de verificação
- **Build:** `dotnet build TickestPristine.slnx --no-incremental` em 2026-10-27: 0 avisos, 0 erros.
- **Testes:** `dotnet test TickestPristine.slnx --logger trx` em 2026-10-27, com o Docker ligado: 1.027 aprovados e 0 com falha (50 de arquitetura, 616 unitários e 361 de integração). Todos os testes citados na rastreabilidade aparecem como `Passed` nos relatórios TRX dessa execução.
- **Auditoria:** `python scripts/auditoria_sdd.py .` em 2026-10-27: score 100,0%, CONFORME, 0 falhas críticas, código de saída 0.
- **Revisão:** `ca-review` sobre `origin/main...HEAD` (T-6, sem mudança de código depois dela): **aprovada, sem achados bloqueantes** nem violações de convenção. Não bloqueantes: o `UPDATE` da última atividade feito depois do commit (mensagens e anexos, DEC-11) não passa pela auditoria; o teste do AC-9 exercita a migration pelo `IMigrator`, e não por HTTP.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` (na T-5, conferido nesta verificação): 2.4 (`LastActivityAtUtc` e o índice novo), 2.6 (RN-31), 2.7 (C-13), 4.3.1 (`status` e `search` em `GET tickets/mine`) e 4.7 (concorrência, DEC-11).
- **CI:**
- **Entrega:** PR #32 (https://github.com/rafaelps15/tickest-pristine/pull/32); a spec fica `Entregue` quando o responsável mergear o PR na `main`.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir do pedido de Rafael Santos no chat do front-end ("Meus Chamados"). A 014 já estava ocupada (`014-primeiro-acesso-chamados-em-aberto`), então esta ficou com a 015; as regras de escrita do mesmo brief ficam reservadas para a 016 (DEC-4). |
| 2026-10-27 | Em descoberta | Duas rodadas de perguntas respondidas no chat (DEC-1 a DEC-10). Aguardando aprovação. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat do front-end ("vamos aprovar as duas"), junto com a 016. Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. |
| 2026-10-27 | Em implementação | `/spec-implement 015` na branch `feat/SPEC-015-own-tickets-listing`, criada a partir da `origin/main` (com a spec 008, PR #30). O texto colado junto do comando repetia o pedido original (MediatR, código na spec, rota e filtro `Open`/`Closed`/`All`); valem as decisões aprovadas (DEC-1, DEC-3, DEC-7). |
| 2026-10-27 | Em implementação | T-1 e T-2 commitadas; DEC-11 registrada (resposta "a" de Rafael Santos). Na T-3, a busca usa `Contains` sobre o título, que é `citext` (`TicketConfiguration.cs`), como `GET tickets`, `GET users` e a consulta de atendentes da spec 009, em vez do `ILIKE` citado no plano: o resultado é o mesmo (sem diferenciar maiúsculas, `%` e `_` tratados como texto), e a Application não referencia o pacote do Npgsql. REQ-4 e AC-6 sem mudança. |
| 2026-10-27 | Em implementação | T-3 a T-5 commitadas. T-6: `ca-review` sobre `origin/main...HEAD` sem achados bloqueantes. Não bloqueantes: o `UPDATE` da última atividade feito depois do commit (mensagens e anexos, DEC-11) não passa pela auditoria, e a mudança só aparece no registro de auditoria do chamado quando ele é criado ou alterado (dado derivado; a mensagem e o anexo continuam auditados); o teste do AC-9 exercita a migration pelo `IMigrator`, e não por HTTP. |
| 2026-10-27 | Verificada | `spec-verify` aprovada: build com 0 avisos; 1.027 testes aprovados e 0 com falha; auditoria SDD 100% (código de saída 0); AC-1 a AC-10 cobertos por testes que exercitam o comportamento descrito e passaram nesta execução; `ca-review` sem achados bloqueantes; `SYSTEM_SPECIFICATION.md` atualizado na T-5 e conferido. Riscos que continuam: R-1 (troca de status simultânea a uma mensagem pode receber 409), R-2 (a busca percorre os chamados do solicitante) e a última atividade fora da transação para mensagens e anexos (DEC-11). O front precisa consumir os campos novos e os parâmetros `status` e `search`; a spec 016 depende do merge desta. |
