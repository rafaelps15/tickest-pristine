---
id: 019
titulo: Busca também na descrição em "Meus chamados"
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-27
branch: feat/SPEC-019-own-tickets-description-search                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 019 — Busca também na descrição em "Meus chamados"

## 1. Contexto
- **Pedido original:** Rafael Santos, no chat, em 2026-10-27 (item 3 de 5): "Filtros em 'Meus chamados' (`GET /tickets/mine`). Aceitar filtro opcional por status e busca por texto (título e descrição), aplicados antes da paginação, para o total e as páginas refletirem o filtro." Na primeira versão: "Filtro por `Status`. Busca textual em `Title` e `Description`. [...] garantindo que o `TotalCount` retornado e os itens da página reflitam corretamente os filtros aplicados."
- **Relação com a spec 015 (em implementação na branch `feat/SPEC-015-own-tickets-listing`):** a 015 já entrega quase todo este item — filtro `status` por grupo (`Active` = Aberto e Em Andamento; `Finished` = Resolvido, Fechado e Cancelado; REQ-3 e DEC-7 da 015), busca `search` no **título** sem diferenciar maiúsculas (REQ-4) e os dois aplicados antes da paginação, com `totalCount` filtrado (REQ-5, AC-7). A 015 pôs "busca por outro campo além do título" em "Fora do escopo". Esta spec cobre só a diferença: a busca passa a procurar também na descrição. O filtro por status do pedido é o grupo que a 015 já entrega (DEC-2).
- **Fatos do repositório (branch da 015):**
  - `Tickets/GetOwn/GetOwnTicketsQueryHandler.cs`: `Where(t => t.CreatedByUserId == userId && (statuses == null || statuses.Contains(t.Status)) && (search == null || t.Title.Contains(search)))`, depois `OrderByDescending(LastActivityAtUtc).ThenByDescending(Id)`, `.Select(...)` e `ToPagedListAsync`. Tudo num `IQueryable`, traduzido para um comando SQL com `COUNT` e `OFFSET/LIMIT` no banco.
  - `Infrastructure/Tickets/TicketConfiguration.cs:20`: `Title` é `citext` (por isso `Contains` não diferencia maiúsculas). Linha 22: `Description` é `varchar(500)` comum — um `Description.Contains(search)` hoje **diferenciaria** maiúsculas e minúsculas.
  - A 015 registrou (histórico, T-3) que a Application não referencia o pacote do Npgsql, então `EF.Functions.ILike` não está disponível nela.
  - `Endpoints/Tickets/GetOwn.cs` lê `status` como `OwnTicketStatusGroup?`. `GET tickets` (Monitor) lê `status` como lista de `TicketStatus` (`[FromQuery(Name = "status")] TicketStatus[]?`).
  - A descrição tem até 500 caracteres; o volume de chamados é por solicitante (o filtro `CreatedByUserId` usa o índice `(CreatedByUserId, LastActivityAtUtc, Id)` da 015).

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | A busca `search` de `GET tickets/mine` procura o trecho no título **ou** na descrição, sem diferenciar maiúsculas e minúsculas; espaços nas pontas são ignorados e busca em branco equivale a sem busca (regras da 015 mantidas). | pedido; DEC-1 |
| REQ-2 | O filtro por status continua o da 015 (`status=Active` ou `status=Finished`), combinado com a busca nova. | pedido; DEC-2 |
| REQ-3 | Filtros e busca continuam aplicados no banco antes do `Count` e do `Skip/Take`; `totalCount` e as páginas refletem tudo; a ordem continua pela última atividade (015). | pedido |
| REQ-4 | Contrato só aditivo: sem os parâmetros novos, a resposta é a da 015. Continua valendo: só os chamados do usuário do token, com `tickets:view-own`. | 015 REQ-6 |

## 3. Critérios de aceitação
Dados comuns: chamados de Maria "Impressora não imprime" (descrição "Sai papel em branco"), "Acesso ao e-mail" (descrição "Outlook pede a SENHA toda hora") e "Monitor piscando" (descrição "Desde ontem"), em status Aberto, Resolvido e Em Andamento, respectivamente; um chamado de Paulo com descrição "senha bloqueada".

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | os dados comuns | Maria chama `GET tickets/mine?search=senha`, `?search=PAPEL`, `?search=impress` e `?search=%20%20` | "Acesso ao e-mail"; "Impressora não imprime"; "Impressora não imprime"; os três |
| AC-2 | REQ-1, REQ-4 | os dados comuns | Maria busca `senha` | o chamado de Paulo não aparece |
| AC-3 | REQ-2 | os dados comuns | `?status=Active&search=e`; `?status=Finished&search=senha`; `?status=Active&search=senha` | "Impressora" e "Monitor" (os ativos com "e" no título ou na descrição); "Acesso ao e-mail"; lista vazia |
| AC-4 | REQ-3 | os dados comuns | `?search=e&status=Active&pageSize=1` | `totalCount` igual ao número de chamados que atendem os dois filtros; a página 2 traz o próximo pela última atividade |
| AC-5 | REQ-4 | os dados comuns | `GET tickets/mine` sem os parâmetros novos | mesma resposta e ordem da 015 |
| AC-6 | REQ-1 | um banco com chamados existentes | aplica a migration da DEC-1 | as descrições continuam iguais, e a busca na descrição ignora maiúsculas |
| AC-7 | todos | a mudança aplicada | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; todos os testes passam |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Como buscar na descrição sem diferenciar maiúsculas | (a) passar `tickets.description` para `citext`, como o título, e usar `Contains` (migration de troca de tipo); (b) `EF.Functions.ILike` no handler (exige o pacote do Npgsql na Application, o que a 015 evitou); (c) `ToLower()` nos dois lados (impede índice e depende de collation) | (a): mesmo mecanismo do título, de `GET tickets` e de `GET users`; a troca de `varchar(500)` para `citext` não muda os dados | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-2 | O "filtro por status" do pedido é o grupo que a 015 já entrega ou um status exato? | (a) o grupo da 015 basta (`Active`/`Finished`), e esta spec só acrescenta a descrição; (b) acrescentar `statuses` com status exatos, como em `GET tickets` | (a): a tela "Meus Chamados" tem só "Em aberto / Encerrados / Todos" (brief da 015); status exato é visão de quem atende, que já tem `GET tickets` (YAGNI). Se o front pedir depois, `statuses` entra de forma aditiva | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-3 | Spec nova ou revisão da 015 | (a) spec nova, depois do merge da 015; (b) revisar a 015 (mudar REQ-4 leva a 015 para `Em descoberta` e atrasa a entrega) | (a): a 015 segue como aprovada; esta parte da `main` depois do merge dela | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |
| DEC-4 | Busca na descrição também em `GET tickets` (Monitor)? | (a) não, só "Meus chamados"; (b) sim | (a): o pedido é da tela "Meus chamados"; com a coluna em `citext`, estender o Monitor depois é uma linha | Delegado por Rafael Santos ao agente, 2026-10-27, no chat ("responda vc como senior .net") |

## 5. Fora do escopo
- Busca nas mensagens ou nos anexos do chamado; busca por id/código.
- Busca de texto completo (`tsvector`) ou índice trigram (ver R-1).
- Mudanças em `GET tickets`, `GET tickets/mine/summary` e no detalhe (DEC-4).
- Filtro por status exato em `GET tickets/mine` (DEC-2).

## 6. Plano técnico
- **Pré-requisito:** spec 015 mergeada na `main`; branch a partir da `origin/main` atualizada (DEC-3).
- **Arquivos a criar ou alterar:**
  - Infrastructure:
    - `Tickets/TicketConfiguration.cs`: `Description` com `HasColumnType("citext")`, mantendo o `HasMaxLength(500)`.
    - Migration nova (`Ticket_Description_Citext`): `ALTER COLUMN description TYPE citext`. A extensão `citext` já existe (título e usuários).
  - Application:
    - `Tickets/GetOwn/GetOwnTicketsQueryHandler.cs`: a condição da busca passa a `search == null || t.Title.Contains(search) || t.Description.Contains(search)`, no mesmo `Where`. Ordem, projeção e `ToPagedListAsync` não mudam.
    - Comentário do `GetOwnTicketsQuery` (pt-BR) atualizado: a busca procura no título e na descrição.
  - Web.Api: nenhuma mudança (o parâmetro `search` já existe).
  - Testes: `GetOwnTicketsQueryHandlerTests.cs` (unitários) e `TicketsTests.cs` (`GetOwn_*`, integração contra o PostgreSQL, para a parte de maiúsculas que só o `citext` resolve), mais um teste da migration se a implementação achar necessário.
- **Contratos:**

  | Rota | Autorização (sem mudança) | Entrada | Resposta | Status |
  |---|---|---|---|---|
  | `GET tickets/mine` | `tickets:view-own` | paginação; `status` (grupo, 015); `search` (agora título ou descrição) | `PagedResponse<OwnTicketResponse>` da 015, sem mudança | 200 · 400 (`status` inválido) · 401 · 403 |

  Exemplo de requisição:

  ```http
  GET /tickets/mine?status=Active&search=senha&page=1&pageSize=10
  ```

  Exemplo de resposta (item no formato da 015):

  ```json
  {
    "items": [
      {
        "id": "a1f0…",
        "title": "Acesso ao e-mail",
        "description": "Outlook pede a SENHA toda hora",
        "priority": 2,
        "status": 2,
        "openedByUserId": "…",
        "assignedToUserId": "…",
        "assignedToName": "Ana Souza",
        "sectorId": "…",
        "sectorName": "Helpdesk",
        "createdAtUtc": "2026-10-27T12:00:00Z",
        "dueAtUtc": "2026-10-28T12:00:00Z",
        "slaStatus": 1,
        "lastActivityAtUtc": "2026-10-27T14:32:10Z"
      }
    ],
    "page": 1,
    "pageSize": 10,
    "totalCount": 1,
    "totalPages": 1,
    "hasNextPage": false,
    "hasPreviousPage": false
  }
  ```

  Os enums saem como número no JSON (a API não registra `JsonStringEnumConverter`): `priority` 2 = `Medium`, `status` 2 = `InProgress`, `slaStatus` 1 = `OnTime`. Na query string, o `status` aceita o nome (`Active`) ou o número.
- **Mudança no banco:** sim — tipo da coluna `tickets.description` (`varchar(500)` → `citext`, limite mantido).
- **Front-end (`tickest-pristine-front`, tela "Meus Chamados"):**
  - placeholder da busca: "Buscar por título ou descrição";
  - o filtro "Em aberto / Encerrados / Todos" continua igual (015);
  - ao mudar filtro ou busca, voltar para a página 1; debounce da busca (ex.: 300 ms) para não disparar uma requisição por tecla.
- **Documentação de estado afetada:** `SYSTEM_SPECIFICATION.md` 4.3 (parâmetros de `GET tickets/mine`), a RN de "Meus chamados" criada pela 015 e o modelo de dados de `Ticket` (tipo da descrição).
- **Riscos:**
  - R-1: `ILIKE '%termo%'` em duas colunas sem índice, mas restrito aos chamados de um solicitante (índice da 015); custo baixo no volume esperado.
  - R-2: a troca de tipo reescreve a tabela `tickets` num `ALTER TABLE` com bloqueio; em produção, aplicar fora do horário de pico.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | `Description` em `citext`: configuração e migration | AC-6 | `add-entity` | [x] |
| T-2 | Busca no título ou na descrição no handler, combinada com o filtro por grupo | AC-1 a AC-5 | `add-feature` | [x] |
| T-3 | Testes unitários e de integração | AC-1 a AC-6 | `add-tests` | [x] |
| T-4 | Atualizar o `SYSTEM_SPECIFICATION.md` | AC-7 | — | [x] |
| T-5 | Revisão de convenções, build sem avisos e suíte inteira | AC-7 | `ca-review` | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_SearchTitleOrDescriptionIgnoringCase_WhenSearchIsGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_SearchTitleOrDescription_WhenSearchIsGiven` | Contra o PostgreSQL, com os dados comuns: `senha` acha "Acesso ao e-mail" pela descrição "Outlook pede a SENHA toda hora" (só o `citext` faz isso), `PAPEL` acha "Impressora" pela descrição, `impress` acha "Impressora" pelo título e `%20%20` traz os três; a descrição volta com a grafia original. O unitário confere título ou descrição sem depender de maiúsculas. Uma mutação que tirava a descrição da busca foi detectada por 3 casos unitários e 5 de integração. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-2 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_NotReturnOtherUsersTickets_WhenOnlyTheirDescriptionMatches`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_NotReturnOtherUsersTickets_WhenDescriptionMatchesSearch` | O chamado de Paulo, com "senha bloqueada" na descrição, não aparece na busca `senha` de Maria; `totalCount` = 1. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-3 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_CombineDescriptionSearchWithStatusGroup_WhenBothAreGiven`; `tests/TickestPristine.Application.UnitTests/Tickets/GetOwnTicketsQueryHandlerTests.cs` › `Handle_Should_CombineDescriptionSearchWithStatusGroup_WhenBothAreGiven` | `status=Active&search=e` traz Impressora e Monitor (o Monitor só tem "e" na descrição), `status=Finished&search=senha` traz "Acesso ao e-mail" e `status=Active&search=senha` traz lista vazia, com `totalCount` 0. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-4 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_PageDescriptionSearchByLastActivity_WhenStatusAndSearchAreGiven` | `search=e&status=Active&pageSize=1`: `totalCount` 2 nas duas páginas; a página 1 traz a Impressora (atividade mais recente) e a página 2, o Monitor. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-5 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `GetOwn_Should_ReturnAllOwnTicketsByLastActivity_WhenNoFilterIsGiven`; os demais `GetOwn_*` e `Handle_*` da 015, sem mudança | Sem parâmetros, os três chamados de Maria vêm pela última atividade (Impressora, Acesso, Monitor), `totalCount` 3; os testes da 015 passam sem alteração. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-6 | `tests/TickestPristine.IntegrationTests/Tickets/TicketsTests.cs` › `Migration_Should_TurnDescriptionIntoCitextKeepingExistingText` | Desce até a migration anterior (coluna `varchar`), grava um chamado com "Outlook pede a SENHA toda hora", aplica `Ticket_Description_Citext` (coluna `citext`) e volta à última migration; a busca `senha` acha o chamado e a descrição volta igual. O `citext` não guarda limite de tamanho no banco, como no título: os 500 caracteres seguem garantidos pelo validator e pelo modelo do EF. | Aprovado na execução da `spec-verify` de 2026-10-28 |
| AC-7 | `dotnet build TickestPristine.slnx --no-incremental` e `dotnet test TickestPristine.slnx` | 0 avisos e 0 erros; 50 testes de arquitetura, 696 unitários e 393 de integração aprovados, nenhuma falha. | Aprovado na execução da `spec-verify` de 2026-10-28 |

## 9. Evidências de verificação
- **Build:** 2026-10-28, `dotnet build TickestPristine.slnx --no-incremental`: 0 avisos, 0 erros.
- **Testes:** 2026-10-28, `dotnet test TickestPristine.slnx` com relatório TRX por teste: 50 de arquitetura, 696 unitários e 393 de integração aprovados, 0 com falha. Os 9 testes novos da rastreabilidade e os 17 `GetOwn_*` aparecem como `Passed` no TRX. Auditoria `python scripts/auditoria_sdd.py .`: score 100,0%, CONFORME, 0 falhas críticas, código de saída 0.
- **Revisão:** `ca-review` sobre as mudanças da spec: nenhum achado bloqueante, nenhuma violação de convenção. Conferido também que nenhuma consulta ao banco compara a descrição por igualdade: a edição detecta a mudança em memória, com `StringComparison.Ordinal` (`UpdateTicketCommandHandler.cs:33`), então o `citext` não esconde uma alteração só de maiúsculas.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md` 2.7 C-13 (busca na descrição em "Meus chamados"), 4.3.1 (parâmetro `search` de `GET tickets/mine`) e o modelo de dados de `Ticket` (descrição em `citext`), no commit `b06f952` (T-4); conferidas nesta verificação, sem outra seção afetada.
- **CI:** roda no PR para a `main`.
- **Entrega:** pendente (PR ainda não aberto). Em produção, a migration reescreve a tabela `tickets` (R-2): aplicar fora do horário de pico.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-27 | Rascunho | Criada a partir do item 3 do pedido de Rafael Santos no chat. A maior parte do item já está na spec 015 (filtro por grupo, busca no título, filtro antes da paginação); esta cobre a diferença. |
| 2026-10-27 | Em descoberta | Fatos levantados no código da branch da 015; DEC-1 a DEC-4 aguardam confirmação. |
| 2026-10-27 | Em descoberta | Rafael Santos delegou as decisões ao agente ("responda vc como senior .net"). DEC-2 ficou (a): sem filtro por status exato; REQ-2 e AC-3 reescritos para o filtro por grupo da 015. Nenhum `[SUPOSIÇÃO]` pendente. Aguardando aprovação explícita. |
| 2026-10-27 | Aprovada | Aprovada por Rafael Santos no chat ("aprovo 017 a 021"). Checklist de completude conferido: nenhuma pergunta aberta e nenhum `[SUPOSIÇÃO]`. Só começa depois do merge da spec 015 na `main`. |
| 2026-10-28 | Em implementação | `/spec-implement 019` na branch `feat/SPEC-019-own-tickets-description-search`, a partir da `origin/main`, com a spec 015 já mergeada (PR #32). Rafael Santos pediu a implementação completa, seguindo as skills, com o agente respondendo por ele ("atue como senior. siga minhas skills e só pare quando finalizar de implemntar tudo"). |
| 2026-10-28 | Verificada | `spec-verify`: build sem avisos, 1.139 testes aprovados, auditoria 100% (saída 0), AC-1 a AC-7 cobertos por testes que passaram nesta execução, `ca-review` sem achados bloqueantes. |
