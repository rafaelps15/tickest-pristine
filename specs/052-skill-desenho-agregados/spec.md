---
id: 052
titulo: Skill de desenho de agregados
status: Verificada       # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-11-06
branch: chore/SPEC-052-aggregate-design-skill                 # <tipo>/SPEC-NNN-<slug>, com tipo em feat, fix, bugfix, chore, docs, test ou refactor, conforme o CLAUDE.md
pr:                     # número do PR (#n), preenchido num commit do próprio PR; depois do merge, o /spec-status mostra Entregue
---

# 052 — Skill de desenho de agregados

## 1. Contexto
- **Pedido original:**
  - Rafael Santos, 2026-11-06, no chat, sobre a regra de autorização dentro do agregado da referência técnica do projeto: "então vamos inserir OnlyMeetingOrGroupOrganizerCanSetMeetingMemberRolesRule identico o do kamil e adpatar no meu codigo. o dele está muito mais apresentavel ok".
  - No questionário seguinte, ele decidiu registrar a aplicação para as etapas da 049 (DEC-83 da 049) e começar antes a spec da skill de agregados: "Spec da skill de agregados (Recomendado)".
  - Origem da skill: DEC-16 da spec 045, de 2026-10-30: uma skill reutilizável, entregue à parte, que "must teach architectural reasoning and decision-making, not merely prescribe patterns or reproduce the current Tickest implementation".
- **Referências:**
  - **Specs:** DEC-16 da 045; DEC-4, DEC-72 e DEC-83 da 049, com o anexo E, E.8, que traz a forma da regra e os nomes da RN-17 e da RN-18.
  - **Skills do Tickest:** `domain-modeling` (padrões 1 a 10), `add-entity` (`references/persistence.md`, "Coleção num campo privado"), `add-feature` (`references/rich-domain-handler.md`), `add-tests` e `ca-review`.
  - **Biblioteca canônica** (`C:\dev\dotnet-universal-skills`):
    - o `CLAUDE.md` dela: formato, idioma inglês, índice, nenhum autor ou projeto citado e "nada inventado" fora do `docs/decision.md`;
    - o `tools/validate_library.py` e os testes dela.
  - **Tickest:** o `scripts/test_instructions.py`, com a paridade dos blocos de código, o índice, as menções e as frases proibidas.
- **Referências técnicas lidas na descoberta (citadas só pela técnica, sem nomes):**
  - **Regra em classe dentro do agregado:** a regra é uma classe com `IsBroken()` e uma mensagem, e o método do agregado a confere. Nessa referência, a regra quebrada lança exceção, o que a convenção `Result` destas skills não permite.
  - **Regra no próprio método do agregado:** o método faz `if` e devolve `Result.Failure`, sem classe de regra, com as coleções internas num campo privado expostas como `IReadOnlyCollection<T>`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | **Skill nova `aggregate-design`** na biblioteca canônica, em inglês, no formato do `CLAUDE.md` da biblioteca (`SKILL.md` curto e modelos em `references/`), com o domínio genérico das skills (`TodoItem`, `Project`, `User`). Ela ensina a decidir o desenho do agregado: a fronteira e o tamanho; a invariante que vale na transação e a que aceita consistência eventual; a referência a outro agregado por id; a regra que precisa de dados de fora ou de vários agregados (dado passado pelo handler, interface de domínio, serviço de domínio, evento de domínio); as coleções internas; e a concorrência. | pedido; DEC-16 da 045; DEC-1 |
| REQ-2 | **Classe de regra:** uma por invariante, em `Domain/{Feature}/Rules/`, `internal sealed`, com os dados no construtor, `IsBroken()` e o `Error`. Os nomes seguem `Only{Who}Can{Action}Rule`, `{Thing}MustHaveAtLeastOne{Item}Rule` e `{Thing}Cannot{Action}Rule`. Vira classe a regra com nome de negócio próprio que recebe dados de fora (permissões, contagens) ou se repete entre métodos; a regra simples sobre o próprio estado continua `if` com o erro. | DEC-83 da 049; DEC-2 |
| REQ-3 | **Peças do SharedKernel na skill:** `IBusinessRule` (`IsBroken()` e `Error`) e o `Entity.CheckRule(rule)`, que devolve `Result`, sem exceção. Num projeto, elas entram na primeira tarefa que as usar. | DEC-3 |
| REQ-4 | **Concorrência:** o token de concorrência na raiz é o padrão, e o conflito vira 409. Quando a invariante atravessa agregados, um ponto de serialização: o token numa linha comum às operações concorrentes. A trava pessimista aparece só como opção que exige transação explícita, fora do padrão das skills, que não usam unidade de trabalho. | DEC-7 |
| REQ-5 | **Teste da regra pelo método do agregado:** o teste quebra a regra e confere o `Error`, com o estado e os eventos intactos; a classe de regra não tem teste próprio. | DEC-8 |
| REQ-6 | **O mínimo nas outras skills, para nenhuma contradizer a nova:** na `domain-modeling`, o padrão 11 (a classe de regra), com um ponteiro para a skill nova; na `add-tests`, o teste da regra; na `ca-review`, a linha do checklist. | DEC-5 |
| REQ-7 | **Biblioteca:** o padrão novo aprovado em `docs/decision.md` e a skill nos índices (`.claude/skills/README.md` e `README.md`). | `CLAUDE.md` da biblioteca ("Nada inventado" e "Índice") |
| REQ-8 | **Espelho no Tickest:** `.claude/skills/aggregate-design/`, como tradução fiel e genérica em pt-BR; a skill no índice `.claude/skills/README.md`; os pares novos no `scripts/test_instructions.py`. Nas outras skills, o mesmo ajuste do REQ-6. | `CLAUDE.md` do Tickest (convenções); DEC-4 |
| REQ-9 | **Listas do Tickest:** o `CLAUDE.md` (lista das skills de código) e o agente `dotnet-architect` citam a skill nova. | DEC-6 |
| REQ-10 | **Genérica:** nenhuma menção ao Tickest, a outro projeto ou a autor, nas duas árvores. Os nomes decididos para a RN-17 e a RN-18 ficam na 049. | `CLAUDE.md` das duas pastas; `scripts/test_instructions.py` |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1, REQ-7, REQ-10 | a biblioteca com a skill nova | `python tools/validate_library.py` e `python -m unittest discover -s tests -v`, na biblioteca | os dois passam: formato, tamanho, links, menções, exemplos sem `{ get; set; }` e `<summary>` nas interfaces |
| AC-2 | REQ-1 | o `SKILL.md` da skill nova | a leitura | o fluxo de decisão traz, em passos: a invariante; os dados de que ela precisa; se ela cabe num agregado ou atravessa vários; se a consistência é imediata ou eventual; e a concorrência. Os tópicos do REQ-1 aparecem, cada um com o link do modelo (verificação descrita) |
| AC-3 | REQ-2, REQ-3 | os modelos em `references/` | a leitura | aparecem o `IBusinessRule`, o `CheckRule`, uma classe de regra com dado de fora, outra com contagem interna, o método do agregado que as confere e o critério de quando usar classe ou `if` (verificação descrita) |
| AC-4 | REQ-4 | o modelo de concorrência | a leitura | aparecem o token na raiz com o 409, o ponto de serialização e a trava pessimista como exceção que exige transação explícita (verificação descrita) |
| AC-5 | REQ-5, REQ-6 | a `domain-modeling`, a `add-tests` e a `ca-review` | a leitura e a busca | o padrão 11, o teste da regra e a linha do checklist existem; nenhuma das três reprova a classe de regra nem a contradiz (verificação descrita) |
| AC-6 | REQ-8, REQ-9, REQ-10 | o Tickest com o espelho | `python -m unittest discover -s scripts -p "test_*.py"` | passa, com os pares novos: paridade dos blocos de código, índice, nenhuma menção ao projeto nem a autor; o `CLAUDE.md` e o `dotnet-architect` citam a skill nova |
| AC-7 | REQ-1 a REQ-10 | o repositório | `python scripts/auditoria_sdd.py .` e `python -m unittest discover -s .claude/hooks -p "test_*.py"` | auditoria 100%, com código de saída 0; testes do portão verdes |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | A skill de agregados é nova ou uma seção da `domain-modeling`? | **(a) skill nova, que ensina a decidir; o código continua na `domain-modeling` e na `add-entity`** / (b) seção na `domain-modeling` | (a): é o que a DEC-16 da 045 pediu (ensinar a decisão, não só padrões) | Rafael Santos, 2026-11-06 (questionário, rodada 1) |
| DEC-2 | Quando a regra vira classe? | **(a) com critério: nome de negócio próprio e dados de fora ou repetição entre métodos; regra simples fica `if`** / (b) toda regra | (a): os agregados ricos de hoje não precisam ser refeitos | Rafael Santos, 2026-11-06 (questionário, rodada 1) |
| DEC-3 | O SharedKernel ganha peças? | **(a) `IBusinessRule` e `Entity.CheckRule` com `Result`** / (b) só a convenção | (a): o par da referência, adaptado ao `Result` | Rafael Santos, 2026-11-06 (questionário, rodada 1) |
| DEC-4 | O que a 052 entrega? | **(a) só a skill: biblioteca, espelho e paridade; a aplicação fica com a 049** / (b) skill e um exemplo aplicado | (a) | Rafael Santos, 2026-11-06 (questionário, rodada 1) |
| DEC-5 | As outras skills mudam? | **(a) o mínimo: padrão 11, teste da regra e linha do checklist** / (b) só a skill nova | (a): sem isso, a revisão e os testes não enxergam o padrão | Rafael Santos, 2026-11-06 (questionário, rodada 2) |
| DEC-6 | A skill entra nas listas do `CLAUDE.md` e do `dotnet-architect`? | **(a) entra** / (b) não agora | (a) | Rafael Santos, 2026-11-06 (questionário, rodada 2) |
| DEC-7 | Como a skill ensina a concorrência? | **(a) token na raiz e ponto de serialização; trava pessimista só como exceção** / (b) token e trava como padrão / (c) fora da skill | (a): a trava exige transação explícita, que as skills não usam | Rafael Santos, 2026-11-06 (questionário, rodada 2) |
| DEC-8 | Como a classe de regra é testada? | **(a) pelo método do agregado** / (b) a classe e o método | (a): a referência testa a regra pelo comportamento do agregado | Rafael Santos, 2026-11-06 (questionário, rodada 2) |
| DEC-9 | As duas falhas anteriores da biblioteca, que impedem o AC-1, se corrigem aqui ou nas specs donas? São elas: o `add-entity/SKILL.md` com 121 linhas e um bloco de 22, do commit `e7feb3b`; e o teste c8, que espera o exemplo de `Expression` que a mudança pendente da 043 tirou da `query-slice.md` | **(a) corrigir agora na 052: o exemplo `ProjectMemberConfiguration` sai do `SKILL.md` para a seção "Child configuration" do `references/rich-entity.md`, nas duas árvores, com a paridade; o c8 passa a conferir o trecho de SQL com nome (`internal const string IsOverdue` e `{TodoItemSql.IsOverdue}`) e mantém a conferência da `Expression` no padrão 9** / (b) nas specs donas, com a 052 `Bloqueada` até lá | (a): o AC-1 exige a biblioteca verde, e nenhuma das duas correções muda regra: uma muda um exemplo de lugar, a outra acompanha a forma atual do modelo | Rafael Santos, 2026-11-07 (questionário: "Corrigir agora na 052 (Recomendado)"); o lugar do exemplo e as asserções do c8 foram delegados ao agente: "decidia por mim como senior" |

## 5. Fora do escopo
- **Aplicação no código do Tickest:** fica com as etapas da 049 (AZ-3, U2 e fase 3 da G3-a, DEC-83 dela), com as peças no SharedKernel do projeto e os nomes da RN-17 e da RN-18.
- **Refazer os agregados ricos de hoje** (`Ticket`, `Department`, `Sector`) para a classe de regra (DEC-2).
- **A trava pessimista como padrão** (DEC-7).
- **O commit:** o das mudanças da biblioteca que já estavam pendentes antes desta spec, e o desta spec. Os dois são do responsável, que pediu "nao faça commit".

## 6. Plano técnico
- **Arquivos a criar ou alterar, na biblioteca (`C:\dev\dotnet-universal-skills`):**
  - `docs/decision.md` (a decisão do padrão);
  - `.claude/skills/aggregate-design/SKILL.md`;
  - `.claude/skills/aggregate-design/references/rules.md`;
  - `.claude/skills/aggregate-design/references/boundaries.md`;
  - `.claude/skills/aggregate-design/references/concurrency.md`;
  - `.claude/skills/README.md` e `README.md` (índices);
  - `.claude/skills/domain-modeling/SKILL.md` e `references/patterns.md` (padrão 11);
  - `.claude/skills/add-tests/SKILL.md`;
  - `.claude/skills/ca-review/SKILL.md`;
  - `.claude/skills/add-entity/SKILL.md` e `references/rich-entity.md` (o exemplo da configuração do filho, DEC-9);
  - `tests/test_library.py` (o teste c8, DEC-9).
- **Arquivos a criar ou alterar, no Tickest:**
  - o espelho em pt-BR de cada arquivo de skill acima, em `.claude/skills/`, inclusive o da DEC-9;
  - `.claude/skills/README.md`;
  - `scripts/test_instructions.py` (pares);
  - `CLAUDE.md` (lista das skills);
  - `.claude/agents/dotnet-architect.md`.
- **Contratos:** não se aplica.
- **Mudança no banco:** não.
- **Riscos:**
  - A biblioteca já tem mudanças não commitadas, da 043 e da 049, em arquivos que esta spec também altera (`domain-modeling`, `add-tests` e `ca-review`). O commit separado vai exigir commit parcial por trecho.
  - A branch desta spec sai da `main` depois do merge do PR #79, porque o `CLAUDE.md` muda nas duas: `git switch -c chore/SPEC-052-aggregate-design-skill origin/main` leva junto as mudanças não commitadas.
  - O limite de 101 linhas do `SKILL.md`.

## 7. Tarefas
| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Biblioteca: decisão em `docs/decision.md`, skill `aggregate-design` (`SKILL.md` e os três modelos) e índices [REQ-1, REQ-2, REQ-3, REQ-4, REQ-7, REQ-10] | AC-1, AC-2, AC-3, AC-4 | manual (regras do `CLAUDE.md` da biblioteca) | [x] |
| T-2 | Biblioteca: o mínimo na `domain-modeling`, na `add-tests` e na `ca-review` [REQ-5, REQ-6] | AC-1, AC-5 | manual | [x] |
| T-3 | Tickest: espelho em pt-BR, índice e pares no `scripts/test_instructions.py` [REQ-8, REQ-10] | AC-6 | manual | [x] |
| T-4 | Tickest: `CLAUDE.md` e `dotnet-architect` [REQ-9] | AC-6 | manual | [x] |
| T-5 | Verificação: validador e testes da biblioteca, testes de scripts e do portão, auditoria e revisão do texto | AC-1, AC-6, AC-7 | spec-verify | [x] |
| T-6 | Correções da DEC-9, feitas antes da T-5: o exemplo da configuração do filho no `rich-entity.md` das duas árvores e o teste c8 da biblioteca [REQ-7] | AC-1, AC-6 | manual | [x] |

## 8. Rastreabilidade
| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | `python tools/validate_library.py` e `python -m unittest discover -s tests -v`, na biblioteca | O validador aprova as 6 skills: formato, tamanho (o `add-entity/SKILL.md` com 94 linhas, depois da DEC-9), links, menções, exemplos sem `{ get; set; }` e `<summary>` nas interfaces. Os testes da biblioteca passam, com o `test_c8_queries_use_translatable_predicates_not_entity_methods` conferindo o trecho de SQL com nome da `query-slice.md` | aprovado: 478 verificações, código de saída 0; 23 de 23 (2026-11-07) |
| AC-2, AC-3, AC-4 | leitura do `SKILL.md` e dos três modelos (verificação descrita) | O fluxo tem os 8 passos do AC-2, cada tópico com o link do modelo. O `rules.md` traz o contrato, o `CheckRule`, as duas regras (dado de fora e contagem interna), o método e o handler. O `concurrency.md` traz o token na raiz com o 409, o ponto de serialização e a trava como exceção | conferido (2026-11-07, de novo depois das correções da revisão) |
| AC-5 | leitura e busca nas três skills, nas duas árvores | O padrão 11 está na `domain-modeling` (lista, tabela, "Não usado" e `patterns.md`). A linha da classe de regra está na `add-tests`. A `ca-review` tem a regra no domínio rico, nos testes e no "não pode aparecer". Nenhuma das três reprova a classe de regra | conferido (2026-11-07) |
| AC-6 | `python -m unittest discover -s scripts -p "test_*.py"` | paridade dos blocos de código dos 21 pares, todo arquivo mapeado, índice, nenhuma menção ao projeto nem a autor; `CLAUDE.md` e `dotnet-architect` | 14 de 14 (2026-11-07, depois das correções da revisão) |
| AC-7 | `python scripts/auditoria_sdd.py .` e `python -m unittest discover -s .claude/hooks -p "test_*.py"` | auditoria e testes do portão | 100%, código de saída 0; 39 de 39 (2026-11-07, depois das correções da revisão) |

## 9. Evidências de verificação
- **Build:** não se aplica (não há código de `src/` nem de `tests/`). Pela tabela de testes por impacto, só normas: os testes de `scripts` e de `.claude/hooks` e a auditoria, sem build nem testes .NET.
- **Testes (2026-11-07, todos depois da última alteração):**
  - biblioteca: validador com 478 verificações e código de saída 0; 23 de 23 testes. Uma execução anterior com `python -I -X utf8` deu 9 erros, todos de leitura da saída do validador: o processo filho escreve em cp1252 e o pai lia em UTF-8. Na forma documentada no próprio arquivo de testes, passou;
  - Tickest: 14 de 14 em `scripts` e 39 de 39 em `.claude/hooks`.
- **Auditoria (2026-11-07):** `python scripts/auditoria_sdd.py .` com score 100%, "CONFORME", código de saída 0 e nenhuma falha crítica.
- **Revisão (2026-11-07):** a `ca-review` sobre os modelos de código e os textos da spec. Nenhum achado bloqueante. Os 5 desvios de convenção encontrados nos modelos da skill nova foram corrigidos dentro do plano, nas duas árvores, e as verificações acima foram refeitas depois:
  1. **Teste do `rules.md`:** o nome e a asserção dos eventos fugiam do modelo da `add-feature` (`references/tests.md`). Passou a `RemoveMaintainer_Should_ReturnFailureAndKeepState_WhenUserIsTheOnlyMaintainer`, com `DomainEvents.Count.ShouldBe(eventsBefore)`.
  2. **`var` no teste:** trocado pelo tipo explícito (`Guid maintainerId`), como nas outras referências.
  3. **`<summary>` do `RemoveMaintainer`:** não citava a falha de mantenedor inexistente.
  4. **`boundaries.md`:** expunha a coleção como cópia (`[.. _maintainers]`). Passou à forma do `rich-entity.md` (`=> _maintainers`), que o teste c2 confere.
  5. **`concurrency.md`:** descrevia o 409 como comportamento existente do tratador global, que nenhum modelo da biblioteca tem. Passou a mandar acrescentar a conversão, uma vez, onde ela faltar. No Tickest, ela já existe (`src/TickestPristine.Web.Api/Infrastructure/DatabaseFailures.cs:26`).
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`, seção 6.9 (a lista das skills de código e o número de pares da paridade).
- **CI:** a preencher.
- **Entrega:** biblioteca no commit `a0772de` (2026-11-07), só com os trechos da 052, validado antes do commit pelo conteúdo exportado do índice (validador com 430 verificações, código de saída 0; 23 de 23). Tickest: a preencher com o PR.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-11-06 | Rascunho | Spec criada pela `spec-new`, a pedido de Rafael Santos (questionário: "Spec da skill de agregados"). |
| 2026-11-06 | Em descoberta | Primeira rodada de perguntas. |
| 2026-11-06 | Em descoberta | Descoberta concluída em duas rodadas, todas as respostas na opção recomendada (DEC-1 a DEC-8); spec escrita, sem `[SUPOSIÇÃO]`. |
| 2026-11-06 | Aprovada | Aprovada por Rafael Santos no chat, depois das duas rodadas: "faça até finalizar e nao faça commit". |
| 2026-11-07 | Em implementação | T-1 a T-4 feitas pela mesma instrução, sem o `/spec-implement` (as skills ficam fora do portão de `src/` e `tests/`), na working tree da branch da 049; a branch própria sai da `main` antes do commit (plano técnico). |
| 2026-11-07 | Bloqueada | Origem: `Em implementação`. O AC-1 depende de duas falhas da biblioteca, anteriores e fora do escopo: o tamanho da `add-entity` (commit `e7feb3b`) e o teste c8, desatualizado pela mudança pendente da 043. Pergunta ao responsável: corrigir aqui ou nas specs donas. |
| 2026-11-07 | Em implementação | Pendência resolvida pela DEC-9: Rafael Santos escolheu "Corrigir agora na 052 (Recomendado)" e delegou os detalhes ("decidia por mim como senior"). Plano técnico ampliado com os arquivos da DEC-9. **Exceção de processo:** os arquivos novos no plano são mudança grande de escopo pelo critério b da `spec-implement`, que levaria a spec a `Em descoberta`. A escolha explícita no questionário, que nomeava as duas correções, e a delegação valeram como aprovação, sem a volta a `Em descoberta`. |
| 2026-11-07 | Em implementação | T-6 feita (DEC-9) e T-5 executada. A revisão encontrou 5 desvios de convenção, nenhum bloqueante, nos modelos da skill nova; foram corrigidos dentro do plano, e as verificações foram refeitas depois. |
| 2026-11-07 | Verificada | Verificação aprovada: biblioteca verde (validador 478, código de saída 0; 23 de 23); Tickest com 14 de 14 em `scripts` e 39 de 39 no portão; auditoria 100%, código de saída 0; todos os ACs cobertos; revisão sem achado bloqueante. `SYSTEM_SPECIFICATION.md` 6.9 atualizada. O commit é do responsável ("nao faça commit"). |
| 2026-11-07 | Verificada | Commit da 052 na biblioteca, `a0772de`, feito pelo agente com a autorização do responsável: "autorizo o commit da 052 na biblioteca". As mudanças pendentes da 043 e da 049 e o teste c8 ficam para o commit delas; o commit do Tickest fica para depois do merge do PR #79. |
