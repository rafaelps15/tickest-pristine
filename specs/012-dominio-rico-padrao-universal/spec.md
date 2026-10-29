---
id: 012
titulo: Domínio rico como padrão universal das skills .NET
status: Em implementação        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-28      # a aprovação de 2026-10-25 foi da versão desfeita (ver Histórico)
branch: chore/SPEC-012-rich-domain-standard
pr:
---

# 012 — Domínio rico como padrão universal das skills .NET

> Pasta definitiva: `specs/012-dominio-rico-padrao-universal/`. Nesta conversa, esta spec é chamada de "Spec A"; a migração do `Ticket` é a "Spec B" e a skill de seed é a "Spec C" (números definidos na criação de cada uma).

## 1. Contexto

- **Pedido original (2026-10-24/25):** sair do domínio anêmico (regras em handlers e em classes estáticas da Application).
- **Revisão de 2026-10-28 (Rafael Santos, no chat):**
  - domínio rico passa a ser o padrão das skills universais .NET, substituindo explicitamente as orientações anêmicas;
  - `C:\dev\dotnet-universal-skills` é a fonte canônica; o Tickest recebe adaptações documentadas, sem divergência silenciosa;
  - o template `C:\dev\clean-architecture` passa a ser referência técnica, não autoridade;
  - skills da biblioteca em inglês; comentários, `<summary>` e textos ao usuário em pt-BR;
  - sem `OccurredOnUtc` obrigatório;
  - `<summary>` obrigatório em membros públicos e internos de entidades, handlers e abstrações;
  - esta spec estabelece o padrão e adapta as instruções do Tickest; a migração do `Ticket` é a Spec B; estratégia e política de seed são da Spec C.
- **Por que a implementação anterior foi desfeita:** registro único: "desfaça todas alterações da spec 012. nao ficou bom. Vou fazer as outras specs antes de voltar a mexer nela" (2026-10-25). O motivo concreto não está documentado. Para reduzir o risco de repetir o problema, esta versão não muda código de produção e divide o trabalho em tarefas pequenas, cada uma com diff e ok.
- **Estado comprovado (2026-10-28):**
  - `CLAUDE.md` do Tickest: "Eventos de domínio" ("não têm métodos (domínio anêmico)") e "Regras permanentes" (domínio anêmico).
  - Skills do Tickest: `add-entity`, `add-feature` (`command-slice`, `query-slice`, `endpoint`, `tests`, `pagination`), `add-tests`, `ca-review`, `spec-new`, `spec-implement`, `spec-verify`; `add-entity` ensina `{ get; set; }`; não existe `domain-modeling`.
  - Biblioteca: `add-entity` (`rich-entity`, `persistence`), `add-feature` (`abstraction`, `authorization`, `command-slice`, `domain-event-handler`, `endpoint`, `query-slice`, `rich-domain-handler`, `tests`), `add-tests`, `ca-review`, `domain-modeling` (`patterns`). Sem git e sem backup. Validador com 261 linhas; 2 testes.
    - `CLAUDE.md` e `docs/decision.md` da biblioteca proíbem `/// <summary>`; o validador o reprova (linhas 140 e 155).
    - O validador exige as 4 skills do template byte a byte e a seção `## Beyond the template` (103–112, 185–219) e depende de `C:\dev\clean-architecture`.
    - Limites: skill fora do template até 101 linhas, bloco até 16 linhas no `SKILL.md` (25–26, 115–120). Tamanhos: `add-entity` 117, `ca-review` 74, `add-feature` 55, `domain-modeling` 51, `add-tests` 37.
    - Nenhuma menção a seed.
  - `C:\dev\tickest-skills-merge` (sem git, 2026-10-27): tradução pt-BR da biblioteca com adaptações do Tickest.
  - Defeitos do modelo atual: coleção pública mutável; padrão 9 usa método da entidade em queries; sem `Result<T> Create`; sem teste de invariante; `abstraction.md` mostra o handler atribuindo propriedade; título duplicado em `rich-domain-handler.md` (merge).
  - Portão SDD: estado por `session_id`, sem teste com subagente; `sdd_gate.py:70-72` trata todo `git stash …` como mudança de árvore (um comando de leitura com `git stash list` foi bloqueado em 2026-10-28).
  - CI (`build.yml:36,39`): testes dos hooks e `scripts/auditoria_sdd.py`; não há `scripts/test_instructions.py`.
  - `dotnet-architect` (rascunho em `C:\Users\rafae\OneDrive\Desktop\revisao-tickest\`): segue o template anêmico, "Sem XML summary", escopo "solução inteira", cita `C:\dev\[projeto externo]` (inexistente), cita autores, aponta para conteúdo inexistente.
  - README do rascunho anuncia `add-integration-event`, sem `SKILL.md`.
  - Trabalho em paralelo: `feat/SPEC-029-tickets-awaiting-action` com mudanças não commitadas; specs 031 a 035 não entregues.
- **Referências:** `CLAUDE.md` (Arquitetura, Eventos de domínio, Contexto do projeto, Regras permanentes, Política de autonomia); `SYSTEM_SPECIFICATION.md` 6.9.

## 2. Requisitos

| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Antes de qualquer alteração na biblioteca: snapshot independente (`.tgz`), manifesto SHA-256 por arquivo, restauração comprovada e `git init` local sem remoto. Nenhuma tarefa que altera a biblioteca começa antes disso e da confirmação do responsável no momento da execução. | DEC-15; P-4 |
| REQ-2 | Validador da biblioteca: sai a exigência byte a byte e da seção `## Beyond the template`; sai a dependência de `C:\dev\clean-architecture`; os limites atuais (101 e 16) passam a valer para todas as skills; reprova `{ get; set; }` em tipo derivado de `Entity`; exige `<summary>` não vazio em membros públicos e internos de blocos que declaram tipo completo; continua reprovando autores e projetos externos em todos os arquivos. | DEC-16, 19, 26, 29 |
| REQ-3 | O modelo canônico ensina o domínio rico no corpo principal das skills, sem exemplo anêmico conflitante, cobrindo C-1 a C-10. | pedido; DEC-16, 17 |
| REQ-4 | Responsabilidades explícitas: entidade ou agregado (invariantes e transições), objeto de valor (valor com regra própria e vários usos), serviço de domínio (regra entre agregados, sem infraestrutura), handler (carrega, autoriza, coordena, grava), consulta (predicado traduzível). | pedido |
| REQ-5 | Seed fica fora desta spec (Spec C). Aqui só vale a regra genérica de criar entidade rica pelo seu mecanismo válido (C-3). | DEC-18; DEC-24 |
| REQ-6 | Padrão de comentários: `<summary>` curto em pt-BR obrigatório em membros públicos e internos de entidades, handlers e abstrações, opcional em privados salvo comportamento não óbvio; comentários internos objetivos; nenhum comentário cita SPEC, REQ ou DEC; nenhum `<summary>` existente é removido. | DEC-19 |
| REQ-7 | As skills do Tickest (`add-entity`, `add-feature`, `add-tests`, `ca-review` e a nova `domain-modeling`) são adaptação da biblioteca, conferida pelo teste de paridade (6.5), com as adaptações da 6.3 marcadas no texto. | DEC-15 |
| REQ-8 | O `CLAUDE.md` do Tickest troca a regra anêmica pela de domínio rico, declara o padrão de comentários, lista as adaptações e ganha a seção "Em migração" com o legado anêmico (incluindo os seeders e o código das specs que seguem antes da Spec B), que permanece documentado até ser migrado. | pedido; DEC-22 |
| REQ-9 | `dotnet-architect` em `.claude/agents/`, corrigido conforme a 6.4. | pedido |
| REQ-10 | O `.claude/skills/README.md` do Tickest ganha só a linha da `domain-modeling`; `add-integration-event` não é adotada. | pedido |
| REQ-11 | Teste de arquitetura "entidade declarada rica não tem `set` público": lista vazia (preenchida pela Spec B) e autoteste com fixture. | pedido; DEC-20 |
| REQ-12 | Portão SDD testado com subagente, com evidência real de bloqueio e de liberação. | pedido |
| REQ-13 | Nenhuma entidade, evento, handler, seeder ou contrato existente muda. | pedido |
| REQ-14 | `scripts/test_instructions.py` (6.5), criado e mantido por esta spec, rodado no CI. Outras specs podem acrescentar pares e diffs esperados de arquivos que elas criam, sem alterar o método nem as entradas existentes. | DEC-28; `spec-verify` |

## 3. Critérios de aceitação

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | 1 | o snapshot | extraído numa pasta temporária e comparado com o manifesto | todos os hashes conferem; comando e saída na seção 9 |
| AC-2 | 2, 6 | a biblioteca depois da T-4c | `python tools/validate_library.py` e `python -m unittest discover -s tests -v` | `RESULTADO: APROVADO` e 0 falhas; fixtures: entidade com `{ get; set; }` reprovada, DTO aprovado, tipo completo sem `<summary>` ou com `<summary>` vazio reprovado, nome de autor reprovado; o código do validador não referencia `clean-architecture` |
| AC-3 | 3, 4 | as skills da biblioteca | testes em `tests/test_library.py`, um por item C-1 a C-10 | cada teste encontra a construção exigida no arquivo indicado na 6.1 (ex.: `IReadOnlyCollection` no C-2, `Result<` e `Create` no C-3, `Expression<Func<` no C-8) e não há `## Beyond the template` |
| AC-4 | 5, 6 | as skills da biblioteca e do Tickest tocadas por esta spec | teste com lista fechada de frases | nenhuma frase que proíba `<summary>`, nenhum `SPEC-`, `REQ-` ou `DEC-` em exemplos, nenhuma instrução de seed (`UseSeeding`, `HasData`, "seed") |
| AC-5 | 7, 14 | os pares de arquivos da 6.5 | `python -m unittest discover -s scripts -p "test_*.py"` | o diff normalizado de cada par é igual ao diff esperado aprovado (vazio quando não há adaptação); nenhum arquivo de skill fora do mapa |
| AC-6 | 8 | o `CLAUDE.md` do Tickest | caso do `test_instructions.py` | sem regra mandando usar o modelo anêmico; seções "Regras permanentes", "Eventos de domínio", "Desvios" e "Em migração" existem; "Em migração" lista os seeders; sem nome de autor ou projeto externo |
| AC-7 | 9 | `.claude/agents/dotnet-architect.md` | caso do `test_instructions.py` | sem "a solução inteira", "mesmo fora do escopo" e "Sem XML summary"; sem autores no corpo e na `description`; `tools` contém `WebFetch` e `WebSearch` (DEC-30) e o texto subordina fontes externas ao `CLAUDE.md`, à spec e às skills; todo caminho citado existe |
| AC-8 | 10 | o README das skills do Tickest | caso do `test_instructions.py` | cada linha tem `.claude/skills/<nome>/SKILL.md` |
| AC-9 | 11 | os `ArchitectureTests` | `dotnet test tests/TickestPristine.ArchitectureTests` | o teste novo passa com a lista vazia; o autoteste confirma que a fixture com `set` público é reprovada |
| AC-10 | 12 | um subagente executando pela ferramenta PowerShell `Copy-Item tests\<arquivo> tests\<arquivo>` | (a) numa sessão nova, sem `/spec-implement`; (b) noutra sessão, depois de `/spec-implement 012` com a spec `Em implementação` | (a) o hook nega com a mensagem do portão; (b) o hook libera e o erro vem do próprio comando; `git status --porcelain` igual antes e depois; saídas e `session_id` na seção 9. Sem as duas evidências, o AC fica pendente |
| AC-11 | 6, 13 | a branch e a biblioteca | `git status --porcelain` e `git diff origin/main` no Tickest; `git diff` da linha de base na biblioteca | em `src/`: nada; em `tests/`: só o teste e a fixture do REQ-11; nenhuma linha `/// <summary>` removida nos dois repositórios |
| AC-12 | 13 | build e testes | `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos; resultados iguais à linha de base da T-1, falhas preexistentes classificadas à parte |
| AC-13 | 14 | o CI | passo novo no `build.yml` | roda `python -m unittest discover -s scripts -p "test_*.py"` e fica verde no PR |
| AC-14 | 14 | os autotestes do teste de paridade | execução | reprovam: `private set` trocado por `set`; `/// <summary>` removido; `Raise` removido; bloco a mais; diff esperado que deixou de ocorrer. Aprova: só a tradução de um comentário ou de uma mensagem |

## 4. Decisões

Decisões da versão de 2026-10-25 (texto original preservado no histórico; backup em `C:\dev\_spec012_backup.tgz`):

| ID | Opções | Situação em 2026-10-28 | Justificativa |
|---|---|---|---|
| DEC-1 (base nos projetos e artigos de um autor) | conforme o texto original (backup) | Superada pelas DEC-15 e DEC-16 | A autoridade é a biblioteca canônica |
| DEC-2 (`UserCredential` separada e rica) | conforme o texto original (backup) | Adiada | Agregado `User` |
| DEC-3 (objeto de valor só para valor com regra própria e vários usos) | conforme o texto original (backup) | Mantida como princípio; o `Email` fica adiado | — |
| DEC-4 (sem guards; lógica copiada em cada handler) | conforme o texto original (backup) | Revista pela DEC-21 | Risco de divergência entre até 13 handlers |
| DEC-5 (construtor primário) | conforme o texto original (backup) | Mantida | — |
| DEC-6 (403 continua 403) | conforme o texto original (backup) | Mantida, aplicada na Spec B | — |
| DEC-7 e DEC-8 (SLA) | conforme o texto original (backup) | Adiadas | Outro agregado |
| DEC-9 (histórico, `UserCodes`, paginação) | conforme o texto original (backup) | Histórico: revisar na Spec B; demais adiadas | — |
| DEC-10 (`add-domain-behavior`) | conforme o texto original (backup) | Superada pela `domain-modeling` | — |
| DEC-11 (`MaxLength` como constante) | conforme o texto original (backup) | Mantida | — |
| DEC-12 (uma branch e um PR) | conforme o texto original (backup) | Mantida | — |
| DEC-13, DEC-14 e REQ-4(a) antigo | conforme o texto original (backup) | Superadas pela DEC-20 | — |
| REQ-8 antigo (normalizar e-mail) | conforme o texto original (backup) | Adiado | — |
| REQ-12 antigo (comentário não cita spec) | conforme o texto original (backup) | Mantido no REQ-6 | — |
| REQ-13 antigo (revisar specs 004–011) | conforme o texto original (backup) | Substituído pela DEC-22 | — |
| Merge: `bool` para "pode não mudar nada" | — | Mantida | — |
| Merge: histórico criado pelo agregado | — | Mantida com salvaguarda (Spec B) | — |
| Merge: `IFileContentInspector` | — | Adiada (DEC-23) | — |
| Merge: `TicketSlaClock` | — | Revisar na Spec B | — |
| Merge: conversor UTC e auditoria no `SaveChanges` | — | Mantida na biblioteca; o Tickest não adota agora (A-5) | — |

Decisões novas:

| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-15 | Fonte da verdade | biblioteca / Tickest | Biblioteca; o Tickest adapta e declara | Rafael Santos, 2026-10-28 |
| DEC-16 | Regra byte a byte | manter / abandonar | Abandonar; o template é referência técnica | Rafael Santos, 2026-10-28 |
| DEC-17 | Idioma | inglês / pt-BR | Skills da biblioteca em inglês, documentação dela em português; comentários, `<summary>` e textos ao usuário em pt-BR; skills do Tickest em pt-BR | Rafael Santos, 2026-10-28 |
| DEC-18 | `OccurredOnUtc` | (a) sem `OccurredOnUtc` / (b) classe base com `OccurredOnUtc` recebido por parâmetro / (c) `DateTime.UtcNow` no SharedKernel | Eventos `record` com ids, sem `OccurredOnUtc`; consumidor futuro usa a hora do processamento; reavaliado na Spec B com o `IDateTimeProvider`. A parte sobre seeders passou para a Spec C | Rafael Santos, 2026-10-28 |
| DEC-19 | `<summary>` | — | Obrigatório em membros públicos e internos de entidades, handlers e abstrações; opcional em privados | Rafael Santos, 2026-10-28 |
| DEC-20 | Teste de arquitetura inicial | estáticas só em `Abstractions/` / entidade rica sem `set` público | Só "entidade rica sem `set` público", por lista explícita, com autoteste | Rafael Santos, 2026-10-28 (pedido) |
| DEC-21 | Autorização | lógica copiada em cada handler / híbrida (entidade, `IPermissionProvider` e handler) | Híbrida: regra de domínio sem consulta na entidade; permissões efetivas pelo `IPermissionProvider`; o handler coordena; consulta de autorização nunca na entidade; regra repetida só vira peça compartilhada quando a Spec B demonstrar a repetição | Rafael Santos, 2026-10-28 |
| DEC-22 | Specs de chamados não entregues (029, 031 a 035) | (a) seguem antes da Spec B / (b) as que não começaram esperam a Spec B | (a): seguem a ordem atual antes da Spec B; o legado fica documentado em "Em migração" até a migração | Rafael Santos, 2026-10-28 |
| DEC-23 | `IFileContentInspector` | nesta spec / spec própria | Spec própria | Rafael Santos, 2026-10-28 |
| DEC-24 | Mecanismo de seed | decidir nesta spec / transferir para a Spec C | Transferida para a Spec C (D-C8 (a) aprovada lá); esta spec não decide nada sobre seed | Rafael Santos, 2026-10-28 |
| DEC-25 | Linha de base no git local da biblioteca | com commit de linha de base / sem | Commit local de linha de base, autorizado | Rafael Santos, 2026-10-28 |
| DEC-26 | `<summary>` em exemplos de documentação | (a) em todo bloco / (b) só em blocos que declaram tipo completo | (b): exigido nos exemplos que declaram tipo completo; a DEC-19 vale integralmente para o código real | Rafael Santos, 2026-10-28 |
| DEC-27 | `exemplos.md` antigo | manter com aviso / remover | Restaurar do backup, com aviso no topo de que está superado e não é referência normativa, e com os nomes de autores externos trocados por `[autor externo]` e os de projetos externos por `[projeto externo]`; o resto do conteúdo fica como no original | Rafael Santos, 2026-10-28 |
| DEC-28 | Local do teste de instruções | `scripts/` / pasta dos hooks do portão | `scripts/test_instructions.py`; o passo no CI depende de autorização na execução | agente, 2026-10-28 (técnica) |
| DEC-29 | Limites do validador | manter 101 e 16 / novos limites (150 e 25) | Manter 101 e 16 para todas as skills; `add-entity` reduzida a até 101 linhas movendo código para `references/` | agente, 2026-10-28 (técnica, medida) |
| DEC-30 | Ferramentas do `dotnet-architect` | manter / remover `WebFetch` e `WebSearch` | Manter `WebFetch` e `WebSearch`; fontes externas são referência técnica, subordinadas ao `CLAUDE.md`, à spec aprovada e às skills canônicas | Rafael Santos, 2026-10-28 |
| DEC-31 | Commits na biblioteca | (a) o agente, depois do ok de cada diff / (b) o responsável | Commits locais por tarefa na biblioteca, só depois da aprovação explícita do diff correspondente; sem push, PR ou operação em outros repositórios; fora desse escopo, continua valendo a regra de não fazer stage | Rafael Santos, 2026-10-28 |
| DEC-32 | Onde trabalhar | (a) worktree a partir da `origin/main` / (b) esperar o commit da 029 | (a): worktree nova a partir da `origin/main`, criada com as autorizações existentes e preservando integralmente as mudanças não commitadas da Spec 029. **Ajuste de 2026-10-29:** a partir da T-4a, a branch fica no repositório principal `C:\dev\tickest-pristine`, para o responsável acompanhar as alterações no Visual Studio; a worktree `C:\dev\tickest-pristine-spec012` ficou no mesmo commit, sem branch (`git switch --detach`), sem nada apagado; as pastas não rastreadas de outras specs no repositório principal não entram nos commits desta spec | Rafael Santos, 2026-10-28; ajuste por Rafael Santos, 2026-10-29 ("b": trazer a branch para o repositório principal; "sim": registrar o ajuste) |
| DEC-33 | Método do teste de paridade | analisador léxico com mutação e corpus / linhas normalizadas com diff esperado | Comparação por linhas normalizadas com diff esperado aprovado (6.5); sem analisador léxico, mutação em massa nem corpus automatizado. Limites aceitos: valores de literais e fidelidade da tradução da prosa não são conferidos | agente, 2026-10-28 (técnica; depende da aprovação desta versão) |

## 5. Fora do escopo

- Converter entidade, evento, handler, seeder ou classe estática existente (Spec B e seguintes).
- `Ensure`, `ValueObject` e extensões de `Result` em `src/` (primeiro uso, na Spec B).
- Estratégia de seed, política de eventos no seed e a skill de seed (Spec C).
- Mensageria e `add-integration-event`.
- Outros agregados, `IFileContentInspector`, normalização de e-mail, SLA.
- Conversor UTC e auditoria no `SaveChanges` do Tickest.
- Rota, payload, status HTTP ou banco.
- Mudança no portão SDD, inclusive a regra do `git stash`; se o AC-10 revelar defeito, a spec para.

## 6. Plano técnico

### 6.0 Pré-condições (bloqueantes)

| ID | Pré-condição | Quando |
|---|---|---|
| P-1 | Nenhuma pasta ou branch usa o número 012; o `C:\dev\_spec012_backup.tgz` é legível | na criação da spec |
| P-2 | Worktree a partir da `origin/main` (DEC-32), sem tocar nas mudanças da 029 | na criação da spec |
| P-3 | Confirmação do responsável no chat para o backup e o `git init`; listagem da biblioteca igual à da T-1 | antes da T-2 |
| P-4 | AC-1 aprovado | antes da T-3 |
| P-5 | Autorização explícita para `.claude/`, `CLAUDE.md` e CI | no início das tarefas que os alteram |
| P-6 | AC-10 aprovado | antes de instalar o agente |

### 6.1 Checklist do modelo canônico

| ID | Item | Onde fica (biblioteca) |
|---|---|---|
| C-1 | Construtor privado com guardas e construtor privado vazio para o EF Core | `add-entity/references/rich-entity.md` |
| C-2 | `private set`; coleção em campo privado exposta como `IReadOnlyCollection<T>` e mapeada pelo campo | `add-entity/references/rich-entity.md`, `add-entity/references/persistence.md` |
| C-3 | `Create` simples quando não pode falhar; `Result<T> Create` quando pode ser rejeitada | `add-entity/references/rich-entity.md`, `domain-modeling/references/patterns.md` |
| C-4 | `Ensure` para erro de programação; `Result` para falha de negócio esperada | `domain-modeling/references/patterns.md` |
| C-5 | Transição confere tudo antes de mudar; em falha, estado e eventos intactos | `domain-modeling/references/patterns.md` |
| C-6 | `Result` para o que pode falhar; `bool` para o que pode não mudar nada | `domain-modeling/references/patterns.md` |
| C-7 | Filho e histórico criados pelo agregado e adicionados pelo handler no mesmo `SaveChangesAsync` | `domain-modeling/references/patterns.md`, `add-feature/references/rich-domain-handler.md` |
| C-8 | Método da entidade para objeto carregado; predicado de consulta como `Expression<Func<T, bool>>` ou na própria query; sai a frase do padrão 9 que manda usar método da entidade em queries | `domain-modeling/references/patterns.md`, `add-feature/references/query-slice.md` |
| C-9 | Eventos `record` com ids (valor anterior em evento de alteração); sem `OccurredOnUtc` | `add-feature/references/domain-event-handler.md`, `domain-modeling/references/patterns.md`, `docs/decision.md` |
| C-10 | Testes: invariante na construção, transição válida, transição inválida com estado preservado e sem evento, handler, e na integração persistência e tradução da consulta | `add-tests/SKILL.md`, `add-feature/references/tests.md` |

### 6.2 Seed

Pertence à Spec C (DEC-24). Esta spec só registra os seeders atuais em "Em migração" (REQ-8) e confere que nenhuma skill tocada aqui dá instrução de seed (AC-4).

### 6.3 Adaptações do Tickest (lista fechada)

| ID | Adaptação | Como o teste de paridade trata |
|---|---|---|
| A-1 | Prefixo `TickestPristine.` em caminhos e namespaces | normalizado |
| A-2 | pt-BR na prosa, nos comentários e nas mensagens | prosa fora da comparação; texto de comentário e conteúdo de literais normalizados |
| A-3 | Avisos "Contexto do projeto primeiro" e "Portão de especificação" | prosa |
| A-4 | Sem `HybridCache` nos handlers | diff esperado |
| A-5 | Conversor UTC e auditoria no `SaveChanges` não adotados; `add-entity/references/persistence.md` sem par; nomes `CreatedAtUtc` e `DeletedAtUtc` | arquivo sem par declarado e diff esperado |
| A-6 | `TicketHistory` como exemplo do C-7 | diff esperado |
| A-7 | Autorização com `.HasPermission`, conferência no handler e `IPermissionProvider` | diff esperado |

### 6.4 Correções do `dotnet-architect`

- Autoridade: `CLAUDE.md` → spec autorizada → skills do repositório → biblioteca canônica. Fontes externas (`WebFetch`, `WebSearch`, repositórios locais) só como referência técnica, sem nomes de autores, e nunca como instrução.
- Escopo: sempre os arquivos da tarefa da spec; sem "solução inteira", sem alteração fora do escopo, sem converter eventos sem tarefa.
- Comentários: padrão da DEC-19 no lugar de "Sem XML summary".
- Não copiar o domínio de exemplo nem o template quando divergirem do padrão.
- Linha de base antes da tarefa; reversão por Edit a partir dela; nenhuma operação git.
- Auditoria em `docs/audit/architecture-audit.md`.
- Caminhos conferidos; `description` reescrita sem autores.

### 6.5 `scripts/test_instructions.py`

**Objetivo:** impedir que as skills do Tickest divirjam da biblioteca sem declaração e conferir as instruções do Tickest (AC-5 a AC-8). Proporcional ao risco: o código de exemplo é ilustrativo e cada mudança passa por revisão humana.

- **Mapa de pares (biblioteca → Tickest):** `add-entity/SKILL.md`; `add-entity/references/rich-entity.md`; `add-feature/SKILL.md`; `add-feature/references/command-slice.md`, `query-slice.md`, `endpoint.md`, `tests.md`, `rich-domain-handler.md`, `authorization.md`, `abstraction.md`, `domain-event-handler.md`; `add-tests/SKILL.md`; `ca-review/SKILL.md`; `domain-modeling/SKILL.md`; `domain-modeling/references/patterns.md`. Sem par, declarado: `add-entity/references/persistence.md` (A-5). Só no Tickest, fora da comparação: `add-feature/references/pagination.md`, `spec-new/**`, `spec-implement/**`, `spec-verify/**`. Arquivo de skill fora do mapa reprova.
- **Extração:** todos os blocos cercados (` ``` `), na ordem.
- **Normalização, linha a linha:** remove o prefixo `TickestPristine.`; colapsa espaços; troca o texto depois de `//` por `//…`, mantendo as tags de `///` (`<summary>`, `</summary>`, `<param …>`, `<returns>`); troca o conteúdo de cada literal `"…"` por `"…"`.
- **Comparação:** `difflib.unified_diff` das linhas normalizadas de cada par. O resultado precisa ser igual ao diff esperado do par, guardado em `scripts/instruction_parity/<par>.diff` e aprovado pelo responsável quando é criado ou muda. Divergência nova reprova; diff esperado que deixou de ocorrer também reprova.
- **Instruções do Tickest:** casos para `CLAUDE.md` (AC-6), agente (AC-7) e README (AC-8), e a lista de frases do AC-4 aplicada às duas árvores.
- **Autotestes:** AC-14.
- **Limites aceitos (DEC-33):** não confere valores de literais (códigos, rotas, mensagens); não confere fidelidade da tradução da prosa nem a qualidade do texto do `<summary>`; um diff esperado aprovado por engano continua aprovado. Esses pontos ficam na revisão humana dos diffs.
- **Execução:** `python -m unittest discover -s scripts -p "test_*.py"`, com passo no `build.yml`.

### 6.6 Arquivos que a implementação pode alterar

- **Biblioteca** (`C:\dev\dotnet-universal-skills`; só depois de P-3 e P-4; diff e ok por tarefa; commit local conforme DEC-31): `CLAUDE.md`, `README.md`, `docs/decision.md`, `docs/installation.md`, `tools/validate_library.py`, `tests/test_library.py`, `.claude/skills/README.md`, `.claude/skills/add-entity/SKILL.md`, `.claude/skills/add-entity/references/rich-entity.md`, `.claude/skills/add-entity/references/persistence.md`, `.claude/skills/add-feature/SKILL.md`, `.claude/skills/add-feature/references/command-slice.md`, `query-slice.md`, `rich-domain-handler.md`, `authorization.md`, `abstraction.md`, `domain-event-handler.md`, `tests.md`, `endpoint.md`, `.claude/skills/add-tests/SKILL.md`, `.claude/skills/ca-review/SKILL.md`, `.claude/skills/domain-modeling/SKILL.md`, `.claude/skills/domain-modeling/references/patterns.md`; criação de `.git/`.
- **Backup:** `C:\dev\_backups\dotnet-universal-skills-<data>.tgz` e `C:\dev\_backups\dotnet-universal-skills-<data>.sha256`.
- **Instruções do Tickest (P-5):** `CLAUDE.md`, `.claude/skills/README.md`, `.claude/skills/add-entity/**`, `.claude/skills/add-feature/**`, `.claude/skills/add-tests/**`, `.claude/skills/ca-review/**`; novos `.claude/skills/domain-modeling/**` e `.claude/agents/dotnet-architect.md`.
- **Fluxo SDD:** `specs/012-dominio-rico-padrao-universal/spec.md` e `exemplos.md` (DEC-27); `SYSTEM_SPECIFICATION.md` 6.9 se o AC-10 revelar algo não documentado. As linhas 57 e 267 do `SYSTEM_SPECIFICATION.md` ("domínio anêmico") continuam válidas até a execução da Spec B, porque esta spec não muda o código (REQ-13).
- **Verificação:** novos `scripts/test_instructions.py` e `scripts/instruction_parity/*.diff`; passo novo em `.github/workflows/build.yml` (P-5).
- **Código do Tickest:** só `tests/TickestPristine.ArchitectureTests/` (REQ-11).
- **Contratos:** nenhum. **Banco:** não.

### 6.7 Riscos

| Risco | Mitigação |
|---|---|
| Repetir o que levou ao rollback (motivo desconhecido) | Sem código de produção; tarefas pequenas com diff e ok |
| Biblioteca sem git e sem backup | REQ-1, P-3 e P-4 bloqueiam |
| Teste de paridade deixar passar diferença em literal ou na prosa | Limite aceito (DEC-33); revisão humana dos diffs esperados |
| Custo de manter os diffs esperados | Aceito: toda mudança na biblioteca passa por revisão |
| Código das specs que seguem antes da B no padrão antigo | DEC-22 e "Em migração" |
| Portão inefetivo ou permissivo demais no subagente | AC-10 e P-6 |
| Portão bloqueia `git stash list` | Comportamento registrado; não contornar |
| Conflito com trabalho em paralelo | DEC-32; rebase ou merge só com autorização |
| `add-entity` não caber em 101 linhas | Mover código para `references/`; se não couber, a tarefa para e pergunta |

### 6.8 Relação com outras specs

| Tema | Dona | Papel desta spec |
|---|---|---|
| Padrão de domínio rico, validador, git e backup da biblioteca, `test_instructions.py`, agente, teste de arquitetura, portão | esta spec | define e implementa |
| Estratégia de seed, política de eventos no seed, `add-seed-data` e seus pares no teste de paridade | Spec C | só cita |
| Migração do `Ticket`, implementação da política de seed, lista do REQ-11, reavaliação do `OccurredOnUtc` | Spec B | só cita |

Ordem: esta spec `Entregue` → Spec C aprovada e entregue → Spec B. Esta spec não depende da C.

## 7. Tarefas

| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Linha de base (somente leitura): build e testes do Tickest; validador e testes da biblioteca; listagem da biblioteca | AC-12 | manual, com diff e ok | [x] |
| T-2 | Com P-3: snapshot, manifesto, restauração verificada, `git init` e commit de linha de base | AC-1 | manual, com diff e ok | [x] |
| T-3 | Validador e testes da biblioteca com fixtures; as regras novas contra a biblioteca real só são ligadas na T-4c | AC-2 | manual, com diff e ok | [x] |
| T-4a | Biblioteca: entidade e `domain-modeling` (C-1 a C-6, C-9); `add-entity` com até 101 linhas | AC-3 | manual, com diff e ok | [x] |
| T-4b | Biblioteca: handler, consulta, autorização, abstração e evento (C-7, C-8) | AC-3 | manual, com diff e ok | [ ] |
| T-4c | Biblioteca: testes, revisão, `CLAUDE.md`, `docs/`, README (C-10); liga as regras novas | AC-2, AC-3, AC-4 | manual, com diff e ok | [ ] |
| T-5 | `scripts/test_instructions.py` com autotestes | AC-14 | manual, com diff e ok | [ ] |
| T-6 | Tickest: skills adaptadas, README e diffs esperados aprovados; passo no CI (P-5) | AC-4, AC-5, AC-8, AC-13 | manual, com diff e ok | [ ] |
| T-7 | Tickest: `CLAUDE.md` (P-5) | AC-6 | manual, com diff e ok | [ ] |
| T-8 | Teste do portão com subagente | AC-10 | manual, com diff e ok | [ ] |
| T-9 | `dotnet-architect` em `.claude/agents/` (P-5, P-6) | AC-7 | manual, com diff e ok | [ ] |
| T-10 | Teste de arquitetura e fixture (`/spec-implement`) | AC-9 | add-tests | [ ] |
| T-11 | Verificação, auditoria SDD, revisão independente e documentação de estado | todos, AC-11 | spec-verify | [ ] |

## 8. Rastreabilidade

| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | evidência por comando (`tar -xzf` + `sha256sum -c`) | restauração comprovada pelo manifesto | Aprovado em 2026-10-29: 24/24 `OK` (seção 9) |
| AC-2, AC-3 | `tests/test_library.py` | AC-2 (T-3): `test_rejects_entity_with_public_setter` (entidade com `{ get; set; }` reprovada), `test_accepts_dto_with_public_setter` (DTO aprovado), `test_rejects_missing_summary` e `test_rejects_empty_summary` (tipo e membro sem `<summary>` ou com `<summary>` vazio reprovados), `test_accepts_documented_rich_types` (sem falso positivo), `test_rejects_author_mention_in_any_document` (autor reprovado), `test_validator_has_no_template_dependency` (sem `clean-architecture`), `test_enforces_line_limit_on_every_skill`, `test_new_rules_are_off_by_default`, `test_catches_broken_copy`, `test_passes_on_library`; AC-3 (classe `CanonicalModel`): `test_c1_private_constructor_with_guards_and_empty_one_for_ef`, `test_c2_private_setters_and_read_only_collection_mapped_by_field`, `test_c3_plain_create_and_result_create`, `test_c4_ensure_for_programming_errors_result_for_business_failures`, `test_c5_every_check_before_the_first_change` (todas as falhas antes da primeira atribuição), `test_c6_result_when_it_can_fail_bool_when_it_may_change_nothing`, `test_c9_events_are_records_with_ids_without_occurred_on` (T-4a); C-7, C-8 e C-10 na T-4b e na T-4c | AC-2: 11/11 aprovados em 2026-10-29 com as regras novas só nas fixtures; aprovação final depois da T-4c. AC-3: C-1 a C-6 e C-9 aprovados em 2026-10-29 (18/18 na suíte) |
| AC-4 a AC-8, AC-14 | `scripts/test_instructions.py` | frases, paridade, `CLAUDE.md`, agente, README, autotestes | |
| AC-9 | `tests/TickestPristine.ArchitectureTests` | lista vazia e autoteste | |
| AC-10 | evidência por comando (duas sessões) | bloqueio e liberação reais | |
| AC-11, AC-12 | `git status`, `git diff`, `dotnet build`, `dotnet test` | comparação com a linha de base | |
| AC-13 | execução do CI no PR | passo novo verde | |

## 9. Evidências de verificação

- **Linha de base (T-1, 2026-10-29, `398f95a` sobre a `origin/main` `008eae2`):**
  - Tickest: `dotnet build TickestPristine.slnx` com 0 avisos e 0 erros; `dotnet test TickestPristine.slnx` com 1.282 testes aprovados e 0 falhas (50 de arquitetura, 761 unitários, 471 de integração).
  - Biblioteca: `python tools/validate_library.py` → `RESULTADO: APROVADO` (192 verificações, 5 skills). `python -m unittest discover -s tests -v` → 2 testes, 1 falha preexistente: `test_catches_broken_copy` espera "cita outro projeto ou autor", que o validador não produz nesse cenário; fica para a T-3, que reescreve o validador e os testes.
  - Listagem da biblioteca: 24 arquivos, sem `.git`, com SHA-256 por arquivo (sem `__pycache__/`, que o próprio `unittest` criou em `tests/` durante a T-1 e que o `.gitignore` da biblioteca já ignora). Referência para a P-3.
- **Backup e restauração (T-2, 2026-10-29):**
  - P-3: confirmação de Rafael Santos no chat ("por enquanto prossiga"); listagem da biblioteca igual à da T-1 (24 arquivos, mesmos SHA-256).
  - Snapshot `C:\dev\_backups\dotnet-universal-skills-2026-10-29.tgz` (30.717 bytes, SHA-256 `5adaa1c5f7bb28c27d68e452934fc0af47175989465f26765c9a0217f015f122`), sem `__pycache__/`; manifesto `C:\dev\_backups\dotnet-universal-skills-2026-10-29.sha256` com 24 linhas.
  - AC-1: `tar -xzf` numa pasta temporária e `sha256sum -c` do manifesto → 24 `OK`, 0 falhas, 24 arquivos restaurados.
  - `git init -b main` na biblioteca, sem remoto (`git remote` vazio); commit local de linha de base `c4e3c82` ("chore: linha de base da biblioteca antes da spec 012"), autorizado por Rafael Santos no chat ("sim"), com os 24 arquivos do manifesto e nenhum outro; árvore igual ao manifesto depois do commit.
- **Validador e testes da biblioteca (T-3, 2026-10-29):**
  - Saíram a comparação byte a byte com o template, a seção `## Beyond the template`, a proibição de `/// <summary>` e toda referência a `C:\dev\clean-architecture` (a isenção dos "originais do template" escondia menções a autores e causava a falha preexistente da T-1). A regra de autores e projetos externos vale para todos os `.md` das skills, `README.md`, `CLAUDE.md` e `docs/`.
  - Regras novas atrás de `--enforce-new-rules` (ligadas por padrão na T-4c): 101 linhas para todas as skills; `{ get; set; }` reprovado em tipo derivado de `Entity`; `<summary>` não vazio exigido em tipos e membros públicos e internos (e membros de interface) dos blocos C# que declaram tipo completo. Sem a flag, os limites atuais continuam (250 linhas para `add-entity`, `add-feature`, `add-tests` e `ca-review`; 101 para as demais).
  - `python tools/validate_library.py` → `RESULTADO: APROVADO` (174 verificações). `python -m unittest discover -s tests -v` → 11 testes, 0 falhas, com fixtures em `tests/fixtures/`.
  - Informativo: com `--enforce-new-rules`, a biblioteca real tem 135 erros (membros sem `<summary>`, entidade `Project` com `{ get; set; }` e `add-entity/SKILL.md` acima de 101 linhas), todos conferidos como defeitos reais; ficam para a T-4a a T-4c.
  - T-4a (2026-10-29): `add-entity/SKILL.md` reescrita sem o exemplo anêmico e sem `## Beyond the template` (100 linhas pela contagem do validador); `rich-entity.md` com `_labels` exposto como `IReadOnlyCollection<string>` e `Result<TodoItem> Create`; `persistence.md` com o mapeamento da coleção pelo campo; `patterns.md` com várias conferências antes da primeira mudança, `Create` simples e com `Result<T>`, `Ensure` contra `Result` e a nova seção 10 (eventos `record` com ids, sem `OccurredOnUtc`); `domain-modeling/SKILL.md` com o padrão 10. `<summary>` em pt-BR em todos os tipos e membros públicos e internos desses arquivos. Validador `APROVADO`; 18 testes, 0 falhas; com `--enforce-new-rules`, nenhum erro em `add-entity` e `domain-modeling` (55 restantes, em `add-feature`, para a T-4b e a T-4c).
- **Build:**
- **Testes:**
- **Portão com subagente (bloqueio e liberação):**
- **Revisão:**
- **Documentação atualizada:**
- **CI:**
- **Entrega:**

## 10. Histórico

> As 12 linhas de 2026-10-25 são as originais, com únicas alterações: as referências nominais ao autor externo foram trocadas por `[autor externo]` e as ao projeto externo por `[projeto externo]`. Essas substituições não mudam o sentido do registro; o texto original, sem elas, está preservado no backup `C:\dev\_spec012_backup.tgz`, o que mantém a rastreabilidade.

| Data | Status | Observação |
|---|---|---|
| 2026-10-25 | Rascunho | Spec criada a partir do pedido de Rafael Santos no chat (seção 1). |
| 2026-10-25 | Em descoberta | Descoberta feita no chat entre 2026-10-24 e 2026-10-25: coleta do código (`C:\dev\tickest-coleta`), comparação com o template e o [projeto externo], DEC-1, DEC-2, DEC-3 e DEC-10 decididas pelo responsável. Pendentes: DEC-4, DEC-5, DEC-7 e DEC-9, com `[SUPOSIÇÃO]`. |
| 2026-10-25 | Em descoberta | DEC-13 decidida por Rafael Santos: o catálogo de permissões e o `RoleNames` não vão para o Domain, e sim para `Application/Abstractions/Authorization/`; o tema saiu da DEC-9. |
| 2026-10-25 | Em descoberta | DEC-4, DEC-5, DEC-7 e DEC-9 confirmadas por Rafael Santos no chat ("sim! depois se eu achar que nao ficou bom a gente muda!"); nenhuma `[SUPOSIÇÃO]` pendente. Exemplos da modelagem em `exemplos.md`. Aguardando a aprovação explícita. |
| 2026-10-25 | Em descoberta | DEC-4 revista por Rafael Santos ("eu quero como o [autor externo] faz. ele nao tem nenhum guard"): sem guards nem serviços, lógica no handler. T-5 passa a incluir a sintaxe `Fixed`/`FromEaster` e o `SlaCalendar.Default`. |
| 2026-10-25 | Aprovada | Aprovada por Rafael Santos no chat ("aprovo a spec 012"). Checklist de completude atendido, sem `[SUPOSIÇÃO]` pendente. |
| 2026-10-25 | Em implementação | Implementação iniciada por `/spec-implement 012`, na branch `refactor/SPEC-012-rich-domain-model` criada a partir da `origin/main` (`93a495a`). |
| 2026-10-25 | Em implementação | Ajuste do REQ-1 e do AC-1 aprovado por Rafael Santos no chat ("1"): as regras de domínio no `CLAUDE.md` e nas skills ficam com o motivo técnico, sem nome de autor ou projeto de terceiros, como já pedia a orientação permanente do responsável (2026-10-05: "Não deixe nome de projeto de terceiros"). A coluna "Referência do [autor externo]" da 6.1 continua só nesta spec, como registro da descoberta. T-1 feita. |
| 2026-10-25 | Em implementação | T-2 feita com a autorização de Rafael Santos no chat ("autorizo a T-2"): skill `add-domain-behavior` e linha no `README.md` das skills. |
| 2026-10-25 | Em implementação | T-3 feita com a autorização de Rafael Santos no chat ("autorizo a T-3"): `add-entity`, `add-feature` (`SKILL.md`, `command-slice.md`, `tests.md`), `add-tests` e `ca-review` sem o padrão anêmico, apontando para a `add-domain-behavior`. |
| 2026-10-25 | Em implementação | DEC-14 decidida por Rafael Santos no chat. T-4 feita: `DomainModelTests`, `StaticClassPlacementChecker` e `EntitySetterChecker`, com 22 classes estáticas pendentes e nenhuma entidade rica listada; build sem avisos e 789 testes aprovados (57 de arquitetura, 462 unitários, 270 de integração). |
| 2026-10-25 | Em descoberta | Implementação desfeita a pedido de Rafael Santos no chat ("desfaça todas alterações da spec 012. nao ficou bom. Vou fazer as outras specs antes de voltar a mexer nela"): os commits da T-1 a T-3 (`CLAUDE.md` e skills) e os testes da T-4 foram descartados com a branch `refactor/SPEC-012-rich-domain-model`, e as tarefas voltaram a `[ ]`. A spec volta para revisão antes de nova aprovação. Pontos a rever: o resultado das tarefas não agradou ao responsável; a T-5 depende do código do calendário de SLA da spec 004 (PR #24), que não está na `origin/main`; a DEC-14 e o ajuste do REQ-1 e do AC-1 (sem nomes de terceiros) continuam registrados para a revisão. |
| 2026-10-28 | Em descoberta | Revisão completa: escopo reduzido ao padrão universal e às instruções do Tickest; `Ticket` na Spec B; seed na Spec C; decisões antigas classificadas; DEC-15 a DEC-33; `aprovada-em` limpo; pasta renomeada sem nome de autor; método de paridade simplificado. |
| 2026-10-28 | Aprovada | Aprovada por Rafael Santos no chat ("Aprovo a Spec 012. Registre a aprovação e atualize o histórico, sem iniciar a implementação."), depois da revisão do texto integral gravado e das correções autorizadas (R-2 a R-7, DEC-27 e nota do histórico). Sem `[SUPOSIÇÃO]` pendente; auditoria SDD 100% conforme. A aprovação é documental: a implementação só começa com `/spec-implement 012`. |
| 2026-10-29 | Em implementação | Implementação iniciada por `/spec-implement 012` na branch `chore/SPEC-012-rich-domain-standard`, depois do rebase sobre a `origin/main` (`008eae2`, com as Specs 029 e 031 já mergeadas) feito por Rafael Santos (`! git rebase origin/main`). |
| 2026-10-29 | Em implementação | T-1 a T-3 feitas, com commits autorizados por Rafael Santos no chat. Ajuste da DEC-32 aprovado por ele no chat ("b"; "sim"): a branch passou da worktree para o repositório principal, para acompanhamento no Visual Studio. A partir da T-4a, as alterações ficam sem stage e sem commit até a autorização explícita de cada commit. |
