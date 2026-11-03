---
id: 051
titulo: Regras do fluxo SDD contínuo
status: Verificada        # Rascunho | Em descoberta | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Descartada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-11-03
branch: chore/SPEC-051-continuous-sdd-flow-rules
pr: "#64"
---

# 051 — Regras do fluxo SDD contínuo

## 1. Contexto

- **Pedido original:** o pedido inicial desta spec (levar para as regras versionadas as decisões D-06, D-13, D-14 e D-10 de 03/11, os três atos e a política de comandos por efeito) foi reduzido pelo responsável, no mesmo dia, ao problema de lentidão do SDD (DEC-1), com estas palavras:

  > Quero simplificar o processo para que o Claude Code:
  > 1. Execute somente os testes afetados pelas alterações, sem rodar a suíte completa a cada pequena tarefa.
  > 2. Faça implementação e verificação em sequência, sem interrupções desnecessárias.
  > 3. Corrija automaticamente falhas por até duas tentativas, dentro do escopo aprovado.
  > 4. Evite repetir builds, testes e auditorias já realizados e ainda válidos.
  > 5. Solicite minha intervenção apenas quando realmente precisar de uma decisão ou encontrar um impedimento.

  Ajustes pedidos sobre a proposta:

  > 1. **Build:** cada tarefa pode executar o build após alterações de código, mas a verificação deve reaproveitar o último resultado válido. Não execute outro build se nenhum arquivo relevante tiver mudado desde o anterior.
  > 2. **Integração:** confirme expressamente que testes de integração não rodam durante as tarefas. São executados uma única vez na verificação final.
  > 3. **Seleção de testes:** estabeleça um critério objetivo e conservador para identificar os testes impactados, considerando alterações diretas e dependências relevantes. Se não for possível determinar o impacto com segurança, amplie a execução em vez de omitir testes.

  > Só acrescente a regra de validade da auditoria: ela deve ser reaproveitada nos ciclos de correção enquanto nenhum arquivo abrangido por ela tiver sido alterado. Se houver alteração em arquivo auditado, execute novamente. Não repita a auditoria sem motivo.

- **Referências:**
  - Parada a cada tarefa: `.claude/skills/spec-implement/SKILL.md:40-46` (uma tarefa por vez, com diff, ok e commit), `:71-74` e `:78`; `CLAUDE.md:189`. Esse fluxo veio da spec 007 (DEC-12 dela).
  - Suíte inteira a cada verificação e a cada skill de código: `.claude/skills/spec-verify/SKILL.md:20` (`dotnet test`) e `:40` ("Evidência de uma execução anterior não vale"); `CLAUDE.md:111` ("Validação de uma mudança"); `.claude/skills/add-feature/SKILL.md:32`, `.claude/skills/add-tests/SKILL.md:33`, `.claude/skills/add-entity/SKILL.md:135` e `.claude/agents/dotnet-architect.md:55`.
  - "Execução atual" como condição de `Verificada`: `.claude/skills/spec-new/references/spec-status.md:14` e `:29`; `.claude/skills/spec-verify/SKILL.md:3`, `:25` e `:39`; `CLAUDE.md:190`.
  - A suíte completa já roda no CI em todo PR para a `main` (`.github/workflows/build.yml:50-51`), e a proteção da `main` exige o check `build` (`SYSTEM_SPECIFICATION.md`, 6.8).
  - O que a auditoria lê: `scripts/auditoria_sdd.py:192-239` (instruções e settings), `:243-306` (specs), `:319-335` (últimos 30 commits), `:337-380` (projetos, build e CI), `:384-393` (`.gitignore` e arquivos rastreados) e `:395-412` (varredura de segredos em todo arquivo de texto fora de `tests/`).
  - O portão lê só o primeiro argumento do `/spec-implement` (`.claude/hooks/sdd_gate.py:42`) e libera a escrita enquanto a spec estiver `Em implementação` (`:220-228`).
  - Documento de estado: `SYSTEM_SPECIFICATION.md`, seção 6.9 (itens das linhas 844, 847 e 848).

## 2. Requisitos

| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | A implementação é contínua por padrão: a `spec-implement` executa todas as tarefas da spec em sequência, sem parar entre elas. O modo tarefa a tarefa, pedido pelo argumento `tarefa-a-tarefa` do `/spec-implement` ou explicitamente no chat, faz cada tarefa terminar com o diff e esperar a aprovação do responsável. | pedido (2); DEC-2 |
| REQ-2 | Ao terminar as tarefas, a `spec-implement` segue direto para a verificação da `spec-verify`, sem parada. | pedido (2); DEC-3 |
| REQ-3 | Se a verificação reprovar, o agente corrige e verifica de novo, até 2 ciclos (cada ciclo é uma correção e uma nova verificação), dentro do plano técnico e sem alterar as asserções dos testes da rastreabilidade. Depois do segundo ciclo sem aprovação, para, mantém a spec `Em implementação` e apresenta o que falhou. A `spec-verify` continua sem corrigir código nem testes. | pedido (3); DEC-4 |
| REQ-4 | O agente só pede intervenção diante de uma decisão que a spec não cobre (mudança de escopo, DEC nova), de um impedimento (AC impossível de verificar, spec que contradiz o `CLAUDE.md`, teste que falha por defeito fora do escopo, bloqueio do portão, branch errada) ou dos 2 ciclos esgotados. O fim tem uma parada única, com o diff, as evidências e a mensagem de commit proposta. | pedido (5); DEC-3 |
| REQ-5 | O agente não faz stage, commit nem push. Ele propõe a mensagem completa, no formato `tipo(escopo): descrição [SPEC-NNN]`, com `T00n` só nos checkpoints de tarefa e o trailer de coautoria; o commit e o push são do responsável. | DEC-5 |
| REQ-6 | Em cada tarefa, o build roda só quando a tarefa alterou arquivo que afeta o build, e os testes rodam sem recompilar, só os unitários e os de arquitetura que o critério de impacto seleciona. Os testes de integração não rodam durante as tarefas nem durante as correções. A suíte completa não roda localmente: fica para o CI. | pedido (1); DEC-7, DEC-8, DEC-9 |
| REQ-7 | O `CLAUDE.md` define um critério de seleção objetivo e conservador (seção 6, "Critério de seleção"), com alterações diretas, pastas dependentes e ampliação na dúvida, que prevalece sobre o `dotnet test` integral das skills de código e do agente `dotnet-architect`. | ajuste 3; DEC-6, DEC-7, DEC-10 |
| REQ-8 | Na verificação final, o build é reaproveitado enquanto válido; rodam só os testes selecionados que não têm resultado válido; os testes de integração rodam uma única vez (na nova verificação de um ciclo de correção, só os de integração afetados pela correção); a auditoria roda uma vez por verificação. | pedido (1 e 4); ajustes 1 e 2; DEC-8, DEC-9, DEC-11 |
| REQ-9 | O `CLAUDE.md` define a validade das execuções (seção 6, "Validade"): build, testes e auditoria valem enquanto nada do que cobrem mudar; as anotações da própria verificação na spec e a atualização do documento de estado não os invalidam; só contam as execuções da sessão. A exigência de "execução atual" das skills dá lugar a essa regra. | pedido (4); ajuste 1; ajuste da auditoria; DEC-8, DEC-11 |
| REQ-10 | O texto acrescentado às skills de spec continua genérico: remete ao critério de impacto e à validade "do contexto do projeto", sem nome, caminho, número de spec nem decisão do projeto. O mapa concreto fica só no `CLAUDE.md`. | pedido inicial ("As skills de spec continuam genéricas, sem citar o projeto; os desvios ficam no CLAUDE.md") |
| REQ-11 | Só mudam os arquivos do plano técnico (seção 6) e esta spec. | pedido ("Mantenha o escopo atual e não acrescente outros assuntos"); DEC-1 |
| REQ-12 | As conferências automáticas existentes continuam aprovando: testes de instruções, testes do portão e auditoria. | pedido inicial ("Verificação proporcional"); DEC-12 |

## 3. Critérios de aceitação

| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | a `spec-implement` alterada | leio o `description` do frontmatter, o `argument-hint` e a seção "Execução" | a execução contínua é o padrão, sem parada entre tarefas; o modo `tarefa-a-tarefa` (argumento ou pedido explícito no chat) termina cada tarefa com o diff e espera a aprovação; não existem mais "Execute **uma tarefa por vez**" nem, no `description`, "uma tarefa por vez (implementa, roda build e testes, mostra o diff, espera o ok e commita)" |
| AC-2 | REQ-2, REQ-3 | a `spec-implement` e a `spec-verify` alteradas | leio a seção de verificação e correção da `spec-implement` e as regras da `spec-verify` | ao concluir as tarefas, a `spec-implement` segue direto para a verificação; numa reprovação, corrige e verifica de novo até 2 ciclos, dentro do plano e sem alterar asserções de testes da rastreabilidade (se precisar disso, para e pergunta); depois do segundo ciclo sem aprovação, para com a spec `Em implementação` e apresenta o que falhou; a `spec-verify` continua sem corrigir e devolve a reprovação para a correção da `spec-implement` |
| AC-3 | REQ-4 | a `spec-implement` alterada | leio "Quando parar" e o fim da execução | as paradas são só a decisão que a spec não cobre, os impedimentos do REQ-4, os 2 ciclos esgotados e a parada final única, com o diff total, as evidências e a mensagem de commit proposta; não existe mais o passo "Informe o que foi feito e indique a skill `spec-verify` como próximo passo" |
| AC-4 | REQ-5 | a `spec-implement` e a `spec-verify` alteradas | procuro instruções de stage, commit e push | nenhuma manda o agente fazer stage, commit ou push; as duas mandam propor a mensagem `tipo(escopo): descrição [SPEC-NNN]`, com `T00n` só em checkpoint de tarefa e o trailer de coautoria; não existem mais "Só então faça o stage dos arquivos da tarefa e o commit" (`spec-implement:46`), "Commit só depois do ok do responsável para aquela tarefa" (`:78`), "commite só depois do ok do responsável" (`spec-verify:33`), "commite na mesma branch depois do ok" (`:34`) e "Commit só depois do ok do responsável, só com a spec e a documentação de estado" (`:41`) |
| AC-5 | REQ-6 | a `spec-implement` alterada | leio o passo de build e testes de cada tarefa | o build roda só quando a tarefa alterou arquivo que afeta o build; os testes rodam sem recompilar (`dotnet test --no-build`), só os unitários e os de arquitetura do critério de impacto; o texto diz expressamente que os testes de integração não rodam durante as tarefas nem durante as correções e que a suíte completa fica para o CI |
| AC-6 | REQ-7 | o `CLAUDE.md` alterado | leio o item "Testes por impacto" da seção "Desvios em relação aos exemplos das skills" | o item traz a entrada (arquivos alterados da tarefa e da spec), a tabela de seleção da seção 6 com as 11 linhas e as colunas "Na tarefa" e "Na verificação final", a regra das pastas dependentes (nome do tipo, e o antigo se foi renomeado, como palavra inteira em `src/` e `tests/`; filtro pelo namespace), a regra "na dúvida, amplia" (projeto inteiro, motivo nas evidências, nunca omite testes) e a prevalência sobre `add-feature`, `add-tests`, `add-entity` e o agente `dotnet-architect` |
| AC-7 | REQ-6, REQ-7 | o `CLAUDE.md` alterado | leio a frase "Validação de uma mudança" (`:111`) e os passos 3 e 4 do "Fluxo de desenvolvimento (SDD)" (`:189-190`) | a validação é por impacto, com a suíte completa só no CI; o passo 3 descreve a implementação contínua (ou tarefa a tarefa, a pedido), emendada na verificação, com até 2 ciclos de correção e a parada única; não existe mais "executa uma tarefa por vez com as skills de implementação: build e testes, diff, ok do responsável e commit" |
| AC-8 | REQ-8 | a `spec-verify` alterada | leio o passo 2 do fluxo | o passo usa o critério de impacto no lugar do `dotnet test` inteiro: reaproveita o último build válido e só builda se arquivo que afeta o build mudou depois dele; roda só os testes selecionados sem resultado válido; roda os de integração uma única vez e, na nova verificação de um ciclo de correção, só os de integração afetados pela correção; roda a auditoria uma vez por verificação, reaproveitada nos ciclos de correção conforme a validade |
| AC-9 | REQ-9 | o `CLAUDE.md` alterado | leio a "Validade" do item "Testes por impacto" | ela traz as regras da seção 6 ("Validade"): os arquivos que afetam o build; a validade do build; a validade dos testes; a validade da auditoria (o que ela lê, o reaproveitamento quando a correção só alterou arquivos `.cs` de `tests/` e a nova execução quando alterou arquivo auditado ou entrou commit novo); a exceção das anotações da verificação e do documento de estado; e que só contam as execuções da sessão |
| AC-10 | REQ-9 | a `spec-verify`, o `spec-status.md` e o `CLAUDE.md` alterados | procuro as condições de evidência | "Evidência de uma execução anterior não vale" (`spec-verify:40`) deu lugar à regra de validade do contexto do projeto; "na execução atual" (`spec-status.md:14` e `:29`) virou "numa execução ainda válida", e o mesmo vale para o `description` da `spec-verify` (`:3`), "na execução do passo 2" (`:25`), "nesta execução" (`:39`) e "testes aprovados na execução atual" (`CLAUDE.md:190`) |
| AC-11 | REQ-10 | o diff de `spec-implement/SKILL.md`, `spec-verify/SKILL.md` e `spec-status.md` | procuro, nas linhas acrescentadas, as cadeias `Tickest`, `SPEC-0`, `spec 0`, `D-0` e `C:\dev` | nenhuma ocorrência; as skills remetem ao critério de impacto e à validade "do contexto do projeto" |
| AC-12 | REQ-11 | a branch pronta para a verificação | rodo `git diff --name-only origin/main` e `git --no-optional-locks status --porcelain --untracked-files=all` | os únicos arquivos alterados ou novos são `CLAUDE.md`, `.claude/skills/spec-implement/SKILL.md`, `.claude/skills/spec-verify/SKILL.md`, `.claude/skills/spec-new/references/spec-status.md`, `SYSTEM_SPECIFICATION.md` e `specs/051-regras-fluxo-sdd-continuo/spec.md` |
| AC-13 | REQ-12 | as alterações feitas | rodo `python -m unittest discover -s scripts -p "test_*.py"` (com `DOTNET_SKILLS_LIBRARY` num caminho inexistente, como no CI), `python -m unittest discover -s .claude/hooks -p "test_*.py"` e `python scripts/auditoria_sdd.py .` | todos os testes aprovados e a auditoria com código de saída 0 (score de pelo menos 70 e nenhuma falha crítica) |

## 4. Decisões

| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Escopo da spec | (a) o pedido inicial: D-06 (stage, commit, push, PR e auto-merge), D-13, D-14, D-10, os três atos e a política de comandos por efeito, em oito arquivos e nos testes de instruções; (b) só o necessário para a lentidão do SDD | (b): "Minha orientação foi revisar a proposta para resolver o problema de lentidão do SDD [...] Elimine tudo que não for necessário para alcançar esses resultados." As respostas da primeira rodada sobre os itens retirados não se aplicam (seção 5). | Rafael Santos, 2026-11-03 |
| DEC-2 | Modo de implementação | (a) contínuo por padrão, tarefa a tarefa a pedido (`/spec-implement NNN tarefa-a-tarefa` ou pedido explícito no chat); (b) tarefa a tarefa sempre | (a) | Rafael Santos, 2026-11-03 (resposta 2(ii)) |
| DEC-3 | O que acontece ao fim das tarefas | (a) a `spec-implement` segue direto para a verificação e para uma única vez, no fim; (b) para depois das tarefas, e o responsável chama a `spec-verify` | (a): "Apresente uma única parada final com diff, evidências e mensagem proposta para o commit." | Rafael Santos, 2026-11-03 (resposta 2(i)) |
| DEC-4 | Correção depois de verificação reprovada | (a) automática: cada ciclo é uma correção e uma nova verificação, até 2, sem alterar asserções de testes rastreados nem ampliar o plano; esgotados, para com a spec `Em implementação`; (b) sem correção automática | (a) | Rafael Santos, 2026-11-03 (resposta 2(iii)) |
| DEC-5 | Mensagem dos commits | (a) o agente propõe a mensagem completa, com `T00n` só em checkpoint e o trailer de coautoria; (b) mensagem livre, sem trailer | (a): "Eu mesmo faço o commit e o push." | Rafael Santos, 2026-11-03 (resposta 3) |
| DEC-6 | Conflito com o `dotnet test` integral das skills de código e do agente | (a) desvio no `CLAUDE.md`, que prevalece; (b) editar as skills e o agente | (a) | Rafael Santos, 2026-11-03 (resposta 5(i)) |
| DEC-7 | Que testes unitários rodar | (a) as pastas dos agregados afetados e os testes alterados; o projeto inteiro em mudança transversal (`SharedKernel`, `Abstractions`, DI); mudança só normativa: testes de instruções e do portão e auditoria, sem build nem testes .NET; (b) sempre o projeto de unitários inteiro | (a) | Rafael Santos, 2026-11-03 (resposta 5(ii)) |
| DEC-8 | Build | (a) a tarefa builda depois de alterar código, e a verificação reaproveita o último build válido; (b) build em toda verificação | (a) | Rafael Santos, 2026-11-03 (ajuste 1) |
| DEC-9 | Testes de integração | (a) não rodam nas tarefas e rodam uma única vez na verificação final; (b) rodam nas tarefas que os afetam | (a); na nova verificação de um ciclo de correção, repetem-se só os de integração afetados pela correção (proposta consolidada, aprovada) | Rafael Santos, 2026-11-03 (ajuste 2) |
| DEC-10 | Critério de seleção dos testes | (a) objetivo e conservador: tabela por tipo de arquivo, pastas dependentes pelo nome do tipo e ampliação na dúvida; (b) julgamento do agente a cada caso | (a), com a tabela da seção 6 | Rafael Santos, 2026-11-03 (ajuste 3 e proposta consolidada) |
| DEC-11 | Validade da auditoria | (a) reaproveitada nos ciclos de correção enquanto nenhum arquivo que ela lê mudar, e executada de novo quando mudar; (b) em toda verificação | (a); as anotações da própria verificação na spec e a atualização do documento de estado não a invalidam | Rafael Santos, 2026-11-03 (ajuste da auditoria e proposta consolidada) |
| DEC-12 | Processo desta spec e vigência das regras novas | (a) esta spec segue o processo atual até a entrega, com verificação proporcional; as regras novas valem a partir do merge; (b) adotar as regras novas já nesta spec | (a): tarefa a tarefa, com o diff revisado pelo responsável antes de cada avanço e amostras do texto antes de editar o `CLAUDE.md` ou as skills; campos `branch:` e `pr:`; stage, commit e push do responsável; verificação pelos testes de instruções e do portão, pela auditoria e pela cobertura de todos os ACs, sem build nem testes .NET locais; nenhuma spec existente é migrada | Rafael Santos, 2026-11-03 (pedido inicial e correção do entendimento E1) |
| DEC-13 | Texto da regra de commit na `spec-implement` (surgiu na aprovação das amostras da T-1) | (a) o texto apresentado nas amostras, que também proibia rebase, merge e `git rm` sem autorização; (b) o texto do responsável | (b): "Não faça stage, commit nem push. O responsável executa essas operações utilizando a mensagem de commit proposta." | Rafael Santos, 2026-11-03 ("com este único ajuste [...] O restante está aprovado exatamente como apresentado") |
| DEC-14 | Execução das tarefas restantes desta spec (ajuste da DEC-12, pedido junto com a aprovação da T-1) | (a) tarefa a tarefa, com aprovação entre tarefas, como na DEC-12; (b) contínua, sem parada para aprovação entre tarefas | (b): "Mantenha o fluxo contínuo: não interrompa para pedir minha aprovação entre tarefas." A T-2 e a T-3 seguem sem parada, com os testes de instruções em cada uma; o diff total e as evidências vão para a revisão do responsável ao fim da T-3, e a verificação continua sendo o passo seguinte | Rafael Santos, 2026-11-03 |
| DEC-15 | Referência a rebase, merge e `git rm` na regra `:41` da `spec-verify` (surgiu na revisão do diff da T-1 à T-3) | (a) manter, como no texto aplicado na T-2; (b) remover e usar a mesma redação da `spec-implement` (DEC-13) | (b): "Faça o ajuste pontual na linha 41 de `spec-verify/SKILL.md`, removendo a referência a rebase, merge e `git rm`. Use a mesma redação aprovada para `spec-implement`." | Rafael Santos, 2026-11-03 |

## 5. Fora do escopo

- A política de comandos por efeito e as exceções dela (`git fetch origin` permanente e `git ls-remote`); as exceções de `git update-ref` e `git branch -m` ficam para a spec de ferramentas (fase 3).
- PR e auto-merge (D-06), a D-08 (strict, Update branch e auto-merge), a D-10 (estado `Rascunho` e campos `pr:` e `branch:`), os três atos, a classificação de legado e a atualização da branch com a `main`.
- `.claude/commands/spec-status.md`, `.github/pull_request_template.md`, `scripts/test_instructions.py`, `.claude/skills/spec-new/SKILL.md` e `.claude/skills/spec-new/references/spec-template.md`.
- A limpeza das menções ao projeto que já existem nas skills de spec (`spec-new/SKILL.md:4`, `spec-implement/SKILL.md:4` e `:32`, `spec-status.md:43-44`); elas ficam como estão, inclusive no `argument-hint` da `spec-implement`, que só ganha o modo opcional.
- A seção 6.8 do `SYSTEM_SPECIFICATION.md`.
- As skills de código e o agente `dotnet-architect`: não são editados, porque o desvio do `CLAUDE.md` prevalece (DEC-6).
- Portão, hooks, settings, CI e `scripts/auditoria_sdd.py`.
- Reserva de número e nome provisório; `claude --worktree` e `.gitignore`; conferência automática de regras; verificador e workflows; proteção da `main` e token; memórias; specs existentes; fases 3 e 4.

## 6. Plano técnico

- **Arquivos a criar ou alterar:**
  1. `.claude/skills/spec-implement/SKILL.md` (T-1):
     - frontmatter: o `description` troca "uma tarefa por vez (implementa, roda build e testes, mostra o diff, espera o ok e commita)" pela execução contínua com testes por impacto, verificação em sequência e parada única, sem `: ` no texto, para o YAML continuar válido; o `argument-hint` ganha o modo opcional `tarefa-a-tarefa`, mantendo as aspas simples;
     - "Execução" (`:38-48`): as tarefas seguem na ordem da spec, em sequência contínua por padrão; no modo `tarefa-a-tarefa` (argumento ou pedido explícito no chat, a qualquer momento), cada tarefa termina com o diff e espera a aprovação do responsável. Em cada tarefa: implementar com a skill da coluna "Skill"; rodar o build só se a tarefa alterou arquivo que afeta o build; depois, rodar sem recompilar (`dotnet test --no-build`) só os testes unitários e de arquitetura que o critério de impacto do contexto do projeto seleciona; testes de integração não rodam nas tarefas nem nas correções; a suíte completa fica para o CI; marcar `[x]` e preencher a rastreabilidade. Saem os passos 4 e 5 (`:45-46`); o parágrafo do portão (`:48`) fica;
     - seção nova "Verificação e correção": ao concluir a última tarefa, a skill segue direto para a `spec-verify`; numa reprovação, corrige e verifica de novo, até 2 ciclos, dentro do plano técnico e sem alterar as asserções dos testes da rastreabilidade (se a correção exigir isso ou sair do plano, para e pergunta); depois do segundo ciclo sem aprovação, para com a spec `Em implementação` e apresenta o que falhou;
     - "Quando parar" (`:64-69`): os três casos atuais, que levam a `Bloqueada`, ficam; a seção diz que, fora deles, de uma decisão que a spec não cobre (seção "Mudança de escopo"), do bloqueio do portão, da branch errada ("Antes de começar") e dos 2 ciclos esgotados, o agente não para nem pede confirmação;
     - "Ao terminar" (`:71-74`): a parada final única, com o diff total, as evidências da verificação e a mensagem de commit proposta (`tipo(escopo): descrição [SPEC-NNN]`, com `T00n` só em checkpoint de tarefa e o trailer de coautoria); stage, commit e push são do responsável. Sai o item que indica a `spec-verify` como próximo passo;
     - "Regras" (`:78`): "Não faça stage, commit nem push. O responsável executa essas operações utilizando a mensagem de commit proposta." (DEC-13).
  2. `.claude/skills/spec-verify/SKILL.md` (T-2):
     - frontmatter: o `description` passa a citar os testes por impacto e "numa execução ainda válida", sem `: ` no texto;
     - passo 2 (`:20`): o conjunto do critério de impacto do contexto do projeto no lugar do `dotnet test` inteiro. A skill reaproveita o último build válido e só builda se arquivo que afeta o build mudou depois dele; roda, sem recompilar e com filtro pelos nomes, só os testes selecionados que não têm resultado válido; roda os testes de integração uma única vez e, na nova verificação de um ciclo de correção, só os de integração afetados pela correção; roda a auditoria uma vez por verificação, reaproveitada nos ciclos de correção conforme a validade. As evidências registram quais resultados foram reaproveitados;
     - passo 3 (`:25`): o teste aparece como aprovado "numa execução ainda válida (passo 2)";
     - passos 7 e 8 (`:33-34`): quando chamada pela `spec-implement`, a apresentação é a parada final única dela; a skill propõe a mensagem de commit, e o commit é do responsável; o preenchimento do campo `pr:` no passo 8 continua como está, com o commit do responsável;
     - "Regras": `:38`, a skill continua sem corrigir, e, quando chamada pela `spec-implement`, a reprovação volta para a correção dela; `:39`, "nesta execução" vira "numa execução ainda válida"; `:40`, a regra dá lugar à regra de validade do contexto do projeto; `:41`, "Não faça stage, commit nem push. O responsável executa essas operações utilizando a mensagem de commit proposta.", sem a referência a rebase, merge e `git rm` (DEC-15).
  3. `.claude/skills/spec-new/references/spec-status.md` (T-2): nas linhas 14 e 29, "na execução atual" vira "numa execução ainda válida".
  4. `CLAUDE.md` (T-3):
     - `:111`, "Validação de uma mudança": por impacto, conforme o item "Testes por impacto"; a suíte completa (`dotnet test TickestPristine.slnx`, com Docker) roda no CI. O trecho da migration, na mesma linha, fica;
     - `:189-190`, passos 3 e 4 do fluxo: implementação contínua (ou tarefa a tarefa, a pedido) com testes por impacto, emendada na verificação, até 2 ciclos de correção e a parada única com diff, evidências e mensagem proposta; a `spec-verify` comprova cada critério numa execução ainda válida e roda a auditoria uma vez, reaproveitada conforme a validade;
     - item novo "Testes por impacto" na seção "Desvios em relação aos exemplos das skills" (`:126`), com o critério de seleção e a validade abaixo. O item prevalece sobre o `dotnet test` integral de `add-feature` (`:32`), `add-tests` (`:33`), `add-entity` (`:135`) e do agente `dotnet-architect` (`:55`); dentro de uma spec, o passo de verificação dessas skills é o da tarefa.
  5. `SYSTEM_SPECIFICATION.md` (na verificação): seção 6.9, itens das linhas 844, 847 e 848, com o fluxo contínuo, os testes por impacto e a validade, citando a spec 051.

- **Critério de seleção** (conteúdo do item "Testes por impacto"). A entrada é a lista de arquivos alterados: na tarefa, os dela; na verificação, os da spec. A verificação final junta as linhas que se aplicam e roda só o que não tiver resultado válido.

  | Alteração | Na tarefa | Na verificação final |
  |---|---|---|
  | Teste unitário criado ou alterado | a classe dele | — |
  | Teste de integração criado ou alterado | não roda | a classe dele |
  | Infraestrutura de teste (classes base, fixtures, helpers) | o projeto de unitários inteiro, se for dele | o projeto de integração inteiro, se for dele |
  | `Domain/{A}/` ou `Application/{A}/` | `UnitTests/{A}/` e as pastas dependentes | `IntegrationTests/{A}/` e as pastas dependentes |
  | `Infrastructure/{A}/` ou `Web.Api/Endpoints/{A}/` | os unitários das pastas dependentes | `IntegrationTests/{A}/` e as pastas dependentes |
  | `SharedKernel`, `Application/Abstractions` (inclusive `Behaviors`) ou qualquer `DependencyInjection.cs` | o projeto de unitários inteiro | o projeto de integração inteiro |
  | Infraestrutura compartilhada e borda HTTP: `ApplicationDbContext`, migrations, seeding, interceptors, autenticação e autorização, `Program.cs`, middleware, `GlobalExceptionHandler`, `CustomResults` | os unitários das pastas dependentes | o projeto de integração inteiro |
  | Build e pacotes: `*.csproj`, `*.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig` | o projeto de unitários inteiro | o projeto de integração inteiro |
  | Qualquer arquivo em `src/` | `ArchitectureTests` | — |
  | Sempre | — | os testes da rastreabilidade |
  | Só normas: `CLAUDE.md`, `.claude/` ou `scripts/` | os testes de `scripts` | os testes de `scripts` e de `.claude/hooks` e a auditoria, sem build nem testes .NET |

  - **Pastas dependentes:** para cada `.cs` de produção alterado, o nome do tipo (o nome do arquivo, e o nome antigo, se foi renomeado) é buscado como palavra inteira em `src/` e `tests/`; toda pasta de agregado que o cita entra como afetada. O filtro usa o namespace, que segue a pasta (ex.: `FullyQualifiedName~TickestPristine.Application.UnitTests.{A}`).
  - **Na dúvida, amplia:** se um arquivo não se encaixa em nenhuma linha, se uma ocorrência fica fora das pastas de agregado ou se o impacto é incerto, roda o projeto de teste inteiro correspondente e registra o motivo nas evidências. Nunca omite testes.

- **Validade** (conteúdo do item "Testes por impacto"):
  - Afetam o build: `src/`, `tests/`, `*.csproj`, `*.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `global.json` e `.editorconfig`.
  - O último build vale até um desses arquivos mudar; a verificação não builda de novo se nenhum deles mudou depois dele.
  - Um resultado de teste vale até mudar algum arquivo que, pelo critério de seleção, o afeta.
  - A auditoria cobre tudo o que o `scripts/auditoria_sdd.py` lê: todo arquivo de texto fora de `tests/` (a varredura de segredos), o que inclui `src/`, as specs, as instruções, os settings e o CI; a lista de arquivos rastreados e de projetos de teste; e os últimos 30 commits. Ela vale enquanto nada disso mudar: num ciclo de correção, é reaproveitada se a correção só alterou arquivos `.cs` dentro de `tests/`; se alterou arquivo auditado, ou se entrou commit novo, roda de novo, uma vez, na nova verificação. Fora desses casos, não se repete.
  - As anotações da própria verificação na spec (rastreabilidade, evidências e histórico) e a atualização do documento de estado não invalidam build, testes nem auditoria.
  - Só contam as execuções desta sessão.

- **Contratos** (rotas, payloads, erros, eventos): não se aplica.
- **Mudança no banco:** não.
- **Compatibilidade com o portão:** `/spec-implement NNN tarefa-a-tarefa` abre a implementação da spec NNN, porque o portão lê só o primeiro argumento (`sdd_gate.py:42`); a escrita continua liberada nos ciclos de correção, porque a `spec-verify` mantém a spec `Em implementação` numa reprovação. Nenhuma mudança no portão.
- **Execução desta spec** (DEC-12): processo atual, tarefa a tarefa. Em cada tarefa: amostras do texto antes de editar, a edição, os testes de instruções, o diff para revisão e a aprovação do responsável antes de avançar. Pela DEC-14, depois da T-1 as tarefas seguem sem parada para aprovação, e o diff total e as evidências vão para a revisão do responsável ao fim da T-3. Os testes de instruções rodam com `DOTNET_SKILLS_LIBRARY` num caminho inexistente, como no CI, para não ler nada fora do repositório.
- **Riscos:**
  - uma regressão fora do impacto selecionado passar pela verificação local: o critério é conservador e amplia na dúvida, e a suíte completa continua obrigatória no CI antes do merge (check `build` exigido pela proteção da `main`);
  - reaproveitar um resultado que deixou de valer: a validade é por arquivo, só contam execuções da sessão e, na dúvida, a execução se repete;
  - a correção automática ajustar o teste em vez do código: ela não pode alterar asserções de testes rastreados nem sair do plano, tem no máximo 2 ciclos, e a `spec-verify` continua lendo cada teste;
  - uma falha de integração aparecer só no fim: os 2 ciclos de correção cobrem esse caso, e o container sobe uma vez por verificação;
  - um diff grande na parada única: o modo `tarefa-a-tarefa` continua disponível a pedido;
  - as skills de código continuarem dizendo `dotnet test` integral: o desvio do `CLAUDE.md` prevalece, pela regra da seção "Convenções a manter ao ampliar".

## 7. Tarefas

| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | `.claude/skills/spec-implement/SKILL.md`: execução contínua e modo `tarefa-a-tarefa`; build e testes por tarefa, sem integração; verificação em sequência; até 2 ciclos de correção; paradas; parada final com a mensagem proposta; sem stage, commit nem push | AC-1, AC-2, AC-3, AC-4, AC-5, AC-11, AC-12, AC-13 | — (texto normativo) | [x] |
| T-2 | `.claude/skills/spec-verify/SKILL.md` e `.claude/skills/spec-new/references/spec-status.md`: passo 2 por impacto, com reaproveitamento; integração e auditoria uma vez; validade no lugar da regra da execução anterior; reprovação devolvida à correção; mensagem proposta e commit do responsável; "numa execução ainda válida" | AC-2, AC-4, AC-8, AC-10, AC-11, AC-12, AC-13 | — (texto normativo) | [x] |
| T-3 | `CLAUDE.md`: "Validação de uma mudança", passos 3 e 4 do fluxo e o item "Testes por impacto" (critério de seleção, pastas dependentes, ampliação, validade e prevalência) | AC-6, AC-7, AC-9, AC-10, AC-12, AC-13 | — (texto normativo) | [x] |

## 8. Rastreabilidade

| AC | Teste(s) — arquivo › método | Como o teste cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | Leitura de `.claude/skills/spec-implement/SKILL.md` (verificação descrita) | `description` (`:3`) e `argument-hint` (`:4`) com o modo contínuo e o `[tarefa-a-tarefa]`; "Execução" (`:40`): contínua por padrão e, no modo `tarefa-a-tarefa`, diff, testes, mensagem do checkpoint e aprovação a cada tarefa; "uma tarefa por vez" não aparece mais (T-1) | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-2 | Leitura de `.claude/skills/spec-implement/SKILL.md` e `.claude/skills/spec-verify/SKILL.md` (verificação descrita) | `spec-implement`, "Verificação e correção" (`:52`): segue direto para a `spec-verify`, até 2 ciclos, dentro do plano e sem alterar asserções rastreadas (se precisar, para e pergunta); esgotados, para com `Em implementação` (T-1). `spec-verify`, regra `:38`: continua sem corrigir e devolve a reprovação à correção da `spec-implement` (T-2) | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-3 | Leitura de `.claude/skills/spec-implement/SKILL.md` (verificação descrita) | "Quando parar": os três casos com `Bloqueada` ficam (`:70-73`), e o parágrafo novo (`:75`) limita as demais paradas a decisão não coberta, portão, branch errada e 2 ciclos esgotados; "Ao terminar" (`:79`): parada final única com diff total, evidências e mensagem; o item que indicava a `spec-verify` como próximo passo saiu (T-1) | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-4 | `git grep -n -i -E "stage\|commit\|push"` nas duas skills e leitura de cada ocorrência (verificação descrita) | `spec-implement`: mensagem do checkpoint com `T00n` e trailer (`:40`), mensagem final `[SPEC-NNN]` com trailer (`:79`) e regra `:83` (DEC-13) (T-1); `spec-verify`: mensagem com trailer e stage, commit e push do responsável (`:33`), commit do responsável no passo 8 (`:34`) e regra `:41` (T-2, ajustada pela DEC-15, sem rebase, merge e `git rm`). O `git grep` só acha propostas de mensagem e proibições; nenhum dos cinco trechos antigos aparece | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-5 | Leitura de `.claude/skills/spec-implement/SKILL.md` (verificação descrita) | "Execução", passo 2 (`:45`): build só se a tarefa alterou arquivo que afeta o build; `dotnet test --no-build` só com os unitários e de arquitetura do critério; integração nem nas tarefas nem nas correções; suíte completa no CI (T-1) | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-6 | Leitura do `CLAUDE.md`, seção "Desvios em relação aos exemplos das skills" (verificação descrita) | Item "Testes por impacto" (`CLAUDE.md:147-172`): prevalência sobre `add-feature`, `add-tests`, `add-entity` e o agente `dotnet-architect` (`:147`); entrada e notação dos caminhos (`:148`); tabela de 11 linhas com as colunas "Na tarefa" e "Na verificação final" (`:150-162`); pastas dependentes, com o filtro pelo namespace (`:164`); "Na dúvida, amplia" (`:165`) (T-3) | Aprovado: conferência de texto em 2026-11-03; 11 linhas na tabela |
| AC-7 | Leitura do `CLAUDE.md`, "Mapa do repositório" e "Fluxo de desenvolvimento (SDD)" (verificação descrita) | `CLAUDE.md:111`: validação por impacto, integração só na verificação final e suíte completa no CI; `:215`: implementação contínua (ou `tarefa-a-tarefa`), emendada na verificação, até 2 ciclos e parada única; o texto antigo do passo 3 não aparece mais (T-3) | Aprovado: conferência de texto em 2026-11-03 |
| AC-8 | Leitura de `.claude/skills/spec-verify/SKILL.md` (verificação descrita) | Passo 2 (`:20`): critério de impacto no lugar da suíte; build reaproveitado, refeito sem avisos só se arquivo que afeta o build mudou; só os testes sem resultado válido, sem recompilar; integração uma única vez e, num ciclo de correção, só a afetada; auditoria uma vez por verificação, reaproveitada conforme a validade; as evidências registram o que foi reaproveitado (T-2) | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-9 | Leitura do `CLAUDE.md`, item "Testes por impacto" (verificação descrita) | `CLAUDE.md:166-172`: arquivos que afetam o build; validade do build, dos testes e da auditoria (o que ela lê, reaproveitamento quando a correção só alterou `.cs` de `tests/` e nova execução com arquivo auditado alterado ou commit novo); exceção das anotações da verificação e do documento de estado; só execuções da sessão (T-3) | Aprovado: conferência de texto em 2026-11-03 |
| AC-10 | `git grep -n -E "execução atual\|nesta execução\|execução anterior não vale\|execução do passo 2"` nos três arquivos (verificação descrita) | Nenhuma ocorrência das quatro expressões na `spec-verify` e no `spec-status.md` (T-2) nem no `CLAUDE.md` (T-3); `spec-status.md:14` e `:29`, `spec-verify:3`, `:25` e `:39` e `CLAUDE.md:216` dizem "numa execução ainda válida", e `spec-verify:40` remete à regra de validade | Aprovado: conferência de texto em 2026-11-03, depois do ajuste da DEC-15 |
| AC-11 | `git diff -U0 origin/main` das três skills, com a busca das cadeias do AC nas linhas acrescentadas (verificação descrita) | Nenhuma das cinco cadeias nas linhas acrescentadas da `spec-implement` (T-1), da `spec-verify` e do `spec-status.md` (T-2) | Aprovado: 0 ocorrências em 2026-11-03, depois do ajuste da DEC-15 |
| AC-12 | `git diff --name-only origin/main` e `git --no-optional-locks status --porcelain --untracked-files=all` (verificação descrita) | Depois da atualização da 6.9: alterados o `CLAUDE.md`, o `SYSTEM_SPECIFICATION.md` e as três skills, e nova só esta spec; a lista bate exatamente com a do AC | Aprovado: 2026-11-03, na verificação |
| AC-13 | `scripts/test_instructions.py` (suíte inteira), `.claude/hooks/test_sdd_gate.py` (suíte inteira) e `python scripts/auditoria_sdd.py .` | Na verificação, uma vez cada, depois do ajuste da DEC-15: testes de instruções com `DOTNET_SKILLS_LIBRARY` num caminho inexistente (14 rodados, 12 aprovados e 2 pulados, a paridade com a biblioteca, como no CI); testes do portão (39 de 39 aprovados); auditoria (score 100,0%, nenhuma falha crítica, código de saída 0). Nas tarefas, os testes de instruções também passaram ao fim de cada uma | Aprovado: 2026-11-03 18:22 |

## 9. Evidências de verificação
- **Build:** não se aplica. A mudança é só normativa (linha "Só normas" do critério de seleção): nenhum arquivo que afeta o build mudou, e não houve build nem teste .NET local (DEC-12).
- **Testes:** em 2026-11-03 18:22, uma execução de cada conjunto:
  - testes de instruções (`DOTNET_SKILLS_LIBRARY=./.sem-biblioteca-canonica python -B -m unittest discover -s scripts -p "test_*.py"`): 14 rodados, 12 aprovados, 2 pulados (a paridade com a biblioteca, ausente como no CI), 0 falhas;
  - testes do portão (`python -B -m unittest discover -s .claude/hooks -p "test_*.py"`): 39 rodados, 39 aprovados, 0 falhas;
  - auditoria (`python scripts/auditoria_sdd.py .`): score 100,0% (CONFORME), 0 falhas críticas, código de saída 0;
  - reaproveitado: nada. Os testes de instruções da T-3 perderam a validade com o ajuste da linha 41 da `spec-verify` (DEC-15), e os do portão e a auditoria ainda não tinham rodado.
- **Revisão:** a `ca-review` não se aplica, porque revisa convenções de código e a spec não altera `src/` nem `tests/`. A consistência entre o `CLAUDE.md` e as skills foi conferida pelos ACs 1 a 11. Nenhum achado bloqueante.
- **Documentação atualizada:** `SYSTEM_SPECIFICATION.md`, seção 6.9: os itens do fluxo no `CLAUDE.md`, da `spec-implement` e da `spec-verify`.
- **CI:** roda no PR (`.github/workflows/build.yml`): testes do portão, testes de instruções, auditoria e a suíte completa (restore, build e test).
- **Entrega:** PR #64 (https://github.com/rafaelps15/tickest-pristine/pull/64), o mesmo do campo `pr:`; a spec fica `Entregue` quando o responsável mergear o PR na `main`.

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-11-03 | Rascunho | Spec criada pela `spec-new` a partir do pedido de Rafael Santos, com o número 051 fixado por ele. |
| 2026-11-03 | Em descoberta | Primeira rodada da descoberta (5 perguntas), respondida no mesmo dia. Depois, o responsável reduziu o escopo à lentidão do SDD (DEC-1) e consolidou a proposta com os ajustes de build, integração, seleção de testes e auditoria (DEC-8 a DEC-11). Por decisão dele, o arquivo foi escrito só com a proposta consolidada e a branch ativa. Checklist de completude atendido; aguarda a aprovação explícita do responsável. |
| 2026-11-03 | Aprovada | Aprovada explicitamente por Rafael Santos no chat ("Aprovo formalmente a SPEC-051 conforme o conteúdo apresentado."), sem `[SUPOSIÇÃO]` pendente e com o checklist de completude atendido. A implementação só começa com `/spec-implement 051`, por autorização específica dele. |
| 2026-11-03 | Em implementação | `/spec-implement 051` digitado por Rafael Santos, na branch `chore/SPEC-051-continuous-sdd-flow-rules` (criada a partir da `origin/main`, em `2804b07`). Execução pelo processo atual, tarefa a tarefa (DEC-12), começando pela T-1. |
| 2026-11-03 | Em implementação | Amostras da T-1 aprovadas no chat, com o ajuste do texto da regra de commit (DEC-13). O responsável pediu fluxo contínuo nas tarefas restantes, sem parada para aprovação entre elas (DEC-14). |
| 2026-11-03 | Em implementação | T-1, T-2 e T-3 concluídas sem parada entre elas (DEC-14), com os testes de instruções aprovados ao fim de cada uma. O diff total vai para a revisão do responsável; o próximo passo é a `spec-verify`. |
| 2026-11-03 | Em implementação | Diff da T-1 à T-3 revisado pelo responsável: ajuste da linha 41 da `spec-verify` aplicado (DEC-15); as precisões do `CLAUDE.md` e os registros da spec aprovados. Segue para a `spec-verify`, sem commit. |
| 2026-11-03 | Verificada | `spec-verify`, com uma execução de cada conjunto: testes de instruções (12 aprovados e 2 pulados de 14), testes do portão (39 de 39) e auditoria (100,0%, código de saída 0). Os 13 ACs estão cobertos; a `ca-review` não se aplica (sem `src/` nem `tests/`); a seção 6.9 do `SYSTEM_SPECIFICATION.md` foi atualizada. Sem commit: stage, commit e push são do responsável. |
| 2026-11-03 | Verificada | Commit `f7e10c1` e push feitos por autorização explícita do responsável. PR #64 aberto pela autorização "abra o PR da spec 051", pronto para revisão; o número do PR foi registrado no campo `pr:` e na linha "Entrega". |
