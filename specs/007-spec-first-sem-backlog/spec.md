---
id: 007
titulo: Fluxo spec-first, sem backlog no repositório
status: Em implementação        # Rascunho | Bloqueada | Aprovada | Em implementação | Verificada | Entregue | Cancelada (transições: spec-status.md da skill spec-new)
responsavel: Rafael Santos
aprovada-em: 2026-10-24
branch: chore/SPEC-007-spec-first-sem-backlog
backlog:
---

# 007 — Fluxo spec-first, sem backlog no repositório

## 1. Contexto
- **Pedido original:** `prompt-migracao-spec-first.md` (Rafael Santos, 2026-10-24, no chat; arquivo temporário, fora do Git). A decisão já tomada, que não se reabre: "O backlog sai do repositório. [...] A spec é a unidade de trabalho. [...] O estado de cada item fica na própria spec, e não num arquivo central."
- **Pré-condições (conferidas em 2026-10-24):** os PRs #14, #15 e #16 foram mergeados (`main` em `5ca764b`); não há stash; a cópia `C:\dev\tickest-backlog\BACKLOG-arquivado-2026-10-24.md` existe, com o BL-001 ao BL-035; a auditoria (`python scripts/auditoria_sdd.py .`) dá 100% e código de saída 0.
- **Inventário (Fase A, `git grep` sobre a `origin/main` `5ca764b`):**
  - **Regras ativas que dependem do backlog ou do `BL-n`:** `CLAUDE.md:146` (branch `<tipo>/<BL-n>-…`) e `:152` (o `specs/BACKLOG.md` guarda as demandas); `.claude/skills/spec-new/SKILL.md:15, 19, 21, 28, 53`; `.claude/skills/spec-new/references/spec-template.md:7-8` (campos `branch` e `backlog`); `.claude/commands/auditoria-arquitetural.md:98, 104, 109, 175`.
  - **Documentação de estado que deixa de ser verdade:** `SYSTEM_SPECIFICATION.md:788` (6.9, descrição do backlog) e `:845` (7.5, ordem de execução no backlog).
  - **Spec aberta que cita o backlog:** `specs/004-sla-calendario-util/spec.md:8, 14, 25`.
  - **Histórico, que fica como está:** `specs/001` e `specs/003` (entregues), `SYSTEM_SPECIFICATION.md:224` (C-15) e `:952` (Q-10), os exemplos de nome de branch em `.claude/hooks/test_sdd_gate.py:321-323` e o comentário em `scripts/auditoria_sdd.py:259`.
  - **Sem dependência:** `.claude/settings*.json`, as skills `spec-implement`, `spec-verify`, `ca-review` e `add-*`, `.github/` (workflow, template de PR, CODEOWNERS, dependabot), `README.md`, `tests/` e a auditoria (`RE_COMMIT_RASTREAVEL` já aceita `SPEC-n` e Conventional Commits).
- **Portão SDD (`.claude/hooks/sdd_gate.py`):** identifica a spec pelo `/spec-implement NNN` da mensagem do usuário (`:239`) e libera escrita em `src/` e `tests/` só com `status: Em implementação` e `aprovada-em` preenchido (`:218-226`). Não depende do nome da branch nem do backlog. Ao bloquear, apaga o registro do `/spec-implement` da sessão (`:495-497`), o que aconteceu duas vezes em 2026-10-24.
- **Ciclo de vida atual** (`.claude/skills/spec-new/references/spec-status.md`): `Rascunho`, `Bloqueada`, `Aprovada`, `Em implementação`, `Verificada`, `Entregue` (depois do commit ou do PR autorizados) e `Cancelada`.
- **Fora da migração, por decisão do responsável:** os arquivos `SPEC-TICKEST.md` e `specs/_fontes/gestao-acessos-onboarding.md`.

## 2. Requisitos
| ID | Requisito | Fonte |
|---|---|---|
| REQ-1 | Nenhuma regra ativa (instrução, validação, regex, teste, etapa de CI, comando ou skill) referencia o `specs/BACKLOG.md` ou exige um `BL-n`. As menções a `BL-n` em specs entregues, no `SYSTEM_SPECIFICATION.md` e nos commits antigos ficam como estão, e uma única nota no `SYSTEM_SPECIFICATION.md` explica que `BL-n` era o identificador anterior. | Pedido (seções 1, 4.2 e 8) |
| REQ-2 | O ciclo de vida da spec passa a ser `Rascunho → Em descoberta → Aprovada → Em implementação → Verificada → Entregue`, ou `Descartada` (no lugar de `Cancelada`), mantendo o estado `Bloqueada` e as suas transições. `Rascunho → Em descoberta` é feita pelo agente, ao iniciar a `spec-new`; `Em descoberta → Aprovada`, só pelo responsável, com todas as perguntas respondidas, nenhum `[SUPOSIÇÃO]` pendente e aprovação explícita no chat, registrando `aprovada-em`. `Entregue` significa PR mergeado na `main`. | Pedido (4.1); DEC-2 |
| REQ-3 | A spec registra o número do PR no campo `pr:` do frontmatter, dentro do próprio PR, e fica `Verificada`. Depois do merge, nenhum commit é necessário: o `/spec-status` mostra a spec como `Entregue`, com a data do merge (`entregue-em`), lendo o estado do PR. O campo `backlog:` sai do modelo. | Pedido (4.1); DEC-6 |
| REQ-4 | Identificação: a spec é `SPEC-NNN`, na pasta `specs/NNN-slug/`; a branch segue `<tipo>/SPEC-NNN-slug`, com `tipo` em `feat`, `fix`, `bugfix`, `chore`, `docs`, `test` ou `refactor`; o commit segue `tipo(escopo): descrição [SPEC-NNN T00n]`, em pt-BR, com o trailer de coautoria do agente; o título do PR cita `SPEC-NNN`, e o corpo traz o link para o `spec.md` e a lista de REQs atendidos. A regra vale por escrito no `CLAUDE.md` e nas skills, sem validação nova no CI. | Pedido (4.2); DEC-4, DEC-10 |
| REQ-5 | A `spec-new` tem uma fase de descoberta obrigatória antes de escrever: lê o pedido, o código afetado, as specs relacionadas, o `CLAUDE.md` e o `SYSTEM_SPECIFICATION.md`; faz no máximo 5 perguntas por rodada, separadas em negócio e técnico (cada decisão técnica com 2 a 3 opções, prós e contras, recomendação e motivo, considerando a arquitetura atual); não pergunta o que dá para descobrir no código; para e espera as respostas, repetindo até não restar dúvida relevante; só então escreve a spec. Toda suposição não confirmada fica marcada como `[SUPOSIÇÃO]`, e uma spec com suposição pendente não pode ser aprovada. A skill não lê nem escreve backlog. | Pedido (4.3) |
| REQ-6 | A `spec-implement` só roda com a spec em `Aprovada` ou `Em implementação` (ou `Bloqueada`, pela retomada que já existe) e executa uma tarefa por vez: implementa, roda build e testes, mostra o diff, espera o ok do responsável e commita. Diante de inclusão, remoção ou alteração de REQ, critério de aceite ou DEC numa spec aprovada, ela para e pergunta; nenhuma DEC nova entra sem aprovação explícita do responsável no chat, com motivo e data registrados na spec. Um bloqueio do portão é classificado (correto ou falso positivo) e informado, sem ser contornado. | Pedido (4.4) |
| REQ-7 | Um bloqueio do portão com a spec registrada ainda em `Aprovada` não apaga o registro do `/spec-implement` da sessão: depois desse bloqueio, a escrita volta a ser liberada assim que a spec passar a `Em implementação` com `aprovada-em`. Com a spec em qualquer outro estado fora de `Em implementação` (`Bloqueada`, `Verificada`, `Rascunho` etc.), o comportamento atual continua: o bloqueio apaga o registro, e a escrita só volta com um novo `/spec-implement`. O portão continua identificando a spec pelo `/spec-implement NNN` e continua bloqueando escrita em `src/` e `tests/` sem spec aprovada. | Pedido (4.4); DEC-3, DEC-7, DEC-11 |
| REQ-8 | A `spec-verify` confere cada critério de aceite contra um teste existente e aprovado na execução atual, roda o build sem avisos, o `dotnet test` e o `python scripts/auditoria_sdd.py`, e registra o status e as evidências na spec. | Pedido (4.5) |
| REQ-9 | Um comando `/spec-status`, somente leitura, lê o frontmatter de todas as specs e mostra uma tabela com número, título, status, branch, `aprovada-em`, PR e `entregue-em` (este último a partir do estado do PR, quando o campo `pr:` estiver preenchido). | Pedido (4.6); DEC-5 |
| REQ-10 | A spec 004 (`Rascunho`) perde o campo `backlog:`, e a menção "Item do backlog: BL-004 (`specs/BACKLOG.md`)" passa a citar o BL-004 como identificador anterior, sem mudar requisito, critério nem decisão. | DEC-8 |
| REQ-11 | O `specs/BACKLOG.md` é removido do repositório como última tarefa, depois de comprovar que a cópia em `C:\dev\tickest-backlog\BACKLOG-arquivado-2026-10-24.md` existe e contém todo o conteúdo do arquivo da `main`. Os itens abertos ficam só nessa cópia. | Pedido (1, 6 e 7); DEC-1 |
| REQ-12 | O `SYSTEM_SPECIFICATION.md` passa a descrever o fluxo spec-first (6.9 e 7.5) e ganha a nota sobre o `BL-n`; nada do que já funciona deixa de funcionar: portão, testes, CI e auditoria. | Pedido (1 e 8) |

## 3. Critérios de aceitação
| ID | REQ | Dado | Quando | Então (resultado observável) |
|---|---|---|---|---|
| AC-1 | REQ-1 | a branch da migração com todas as tarefas feitas | roda `git grep -n -i -E "BACKLOG\|BL-[0-9]"` no repositório | todas as ocorrências estão na lista de histórico da seção 6 (specs 001 e 003; na spec 004, as linhas de evidência e de histórico e a menção ao identificador anterior; no `SYSTEM_SPECIFICATION.md`, C-15, Q-10, a nota nova e o registro da migração em 6.9 e 7.5; exemplos de branch em `test_sdd_gate.py`; e esta spec 007); nenhuma em `CLAUDE.md`, `.claude/skills/`, `.claude/commands/`, `.github/` ou `scripts/` |
| AC-2 | REQ-7 | os testes do portão (`.claude/hooks/test_sdd_gate.py`) | roda `python -m unittest discover -s .claude/hooks -p "test_*.py"` | todos aprovados, incluindo os casos já existentes (escrita em `src/` e `tests/` bloqueada sem spec e liberada com spec `Em implementação` e `aprovada-em`), com `test_spec_implement_mode_ends_when_spec_leaves_implementation` sem alteração (depois de sair para `Bloqueada`, `Verificada` ou `Rascunho` e de uma escrita bloqueada, a sessão só volta a escrever com um novo `/spec-implement`), e dois casos novos: (1) depois de um comando bloqueado com a spec ainda `Aprovada`, o registro da sessão continua, e a escrita é liberada quando a spec passa a `Em implementação`; (2) `git switch -c chore/SPEC-007-x` é liberado |
| AC-3 | REQ-2, REQ-3 | o `spec-status.md` e o `spec-template.md` alterados | leitura dos dois arquivos (verificação descrita) | os estados são `Rascunho`, `Em descoberta`, `Aprovada`, `Em implementação`, `Verificada`, `Entregue`, `Bloqueada` e `Descartada`, com as transições e os responsáveis da seção 4.1 do pedido; `Entregue` significa PR mergeado; o modelo tem os campos `branch:` (`<tipo>/SPEC-NNN-slug`) e `pr:`, e não tem `backlog:` |
| AC-4 | REQ-5 | a `spec-new` alterada | leitura da skill (verificação descrita) | a skill tem a fase de descoberta com no máximo 5 perguntas por rodada, separadas em negócio e técnico, a parada à espera das respostas, a regra de não perguntar o que dá para descobrir no código e a regra do `[SUPOSIÇÃO]` que impede a aprovação; não cita backlog |
| AC-5 | REQ-6 | a `spec-implement` alterada | leitura da skill (verificação descrita) | a skill exige spec `Aprovada` ou `Em implementação` (ou a retomada de `Bloqueada`), uma tarefa por vez com build, testes, diff, ok e commit, parada e pergunta diante de mudança de REQ, critério ou DEC, aprovação explícita de DEC nova com motivo e data, a seção "Mudança de escopo" da DEC-13 (ajuste aprovado no chat é registrado e segue; REQ novo, REQ removido ou mudança grande, pelos critérios a a e, volta para `Em descoberta`), coerente com o `spec-status.md` e com a revisão da `spec-new`, e classificação de bloqueio do portão sem contorno |
| AC-6 | REQ-8 | a `spec-verify` alterada | leitura da skill (verificação descrita) | a skill roda o build sem avisos, o `dotnet test` e o `python scripts/auditoria_sdd.py`, e confere cada critério contra um teste aprovado na execução atual |
| AC-7 | REQ-9 | as specs 001 a 004 e 007 no repositório | roda `/spec-status` | uma tabela com uma linha por spec, com número, título, status, branch, `aprovada-em`, PR e `entregue-em`, com os valores iguais aos do frontmatter de cada uma (conferidos com `Grep` por `^status:`); o comando não escreve nenhum arquivo |
| AC-8 | REQ-4 | o `CLAUDE.md` alterado | leitura (verificação descrita) | o padrão de branch é `<tipo>/SPEC-NNN-slug` com os sete tipos, o formato de commit é `tipo(escopo): descrição [SPEC-NNN T00n]`, o PR cita `SPEC-NNN` com o link do `spec.md` e os REQs, a política de autonomia exige mostrar o diff e receber o ok do responsável antes de todo commit (DEC-12), e não há menção ao backlog |
| AC-9 | REQ-10 | a spec 004 alterada | `git diff` da spec 004 | só mudam o campo `backlog:` (removido) e as menções ao backlog nas linhas do contexto; nenhuma linha de REQ, AC ou DEC muda |
| AC-10 | REQ-11 | a cópia externa e o `specs/BACKLOG.md` da `main` | antes do `git rm`, compara cada linha do arquivo da `main` com a cópia | todas as linhas do arquivo da `main` estão na cópia; só então o arquivo é removido, e o `git status` mostra a remoção |
| AC-11 | REQ-12 | a branch da migração | roda `python scripts/auditoria_sdd.py .` | score 100% e código de saída 0 |
| AC-12 | REQ-12 | a branch da migração | roda `dotnet build TickestPristine.slnx` e `dotnet test TickestPristine.slnx` | 0 avisos, 0 erros e todos os testes aprovados |
| AC-13 | REQ-12 | o PR da migração | o CI roda | o job `build` termina verde |
| AC-14 | REQ-1, REQ-12 | o `SYSTEM_SPECIFICATION.md` alterado | leitura das seções 6.9 e 7.5 e da nota (verificação descrita) | as seções descrevem o fluxo spec-first, sem backlog no repositório, citando esta spec; a nota diz que `BL-n` era o identificador anterior e que as menções antigas ficam como registro histórico |

## 4. Decisões
| ID | Pergunta | Opções | Decisão | Decidido por, em |
|---|---|---|---|---|
| DEC-1 | Destino dos itens abertos do backlog | (a) só na cópia externa; (b) virar spec em `Rascunho`; (c) descartar | (a) para todos. O BL-032 vira spec própria depois, fora desta migração | Rafael Santos, 2026-10-24 (chat: "Itens abertos ficam só na cópia externa; BL-032 vira spec própria depois.") |
| DEC-2 | Ciclo de vida | (a) o da seção 4.1 do pedido ao pé da letra; (b) o da 4.1, mantendo `Bloqueada` | (b). `Cancelada` passa a se chamar `Descartada`, como na 4.1 (nenhuma spec usa `Cancelada`) | Rafael Santos, 2026-10-24 (chat: "Ciclo de vida da seção 4.1, mantendo o estado Bloqueada se ele já existe.") |
| DEC-3 | Como o portão identifica a spec | (a) manter o `/spec-implement NNN`; (b) ler o `SPEC-NNN` da branch; (c) os dois | (a). Só o padrão de branch muda, para `<tipo>/SPEC-NNN-slug` | Rafael Santos, 2026-10-24 (chat: "Mantenha o portão identificando a spec pelo /spec-implement NNN; só mude o padrão de branch para <tipo>/SPEC-NNN-slug.") |
| DEC-4 | Formato do commit | — | `tipo(escopo): descrição [SPEC-NNN T00n]` | Rafael Santos, 2026-10-24 (chat: "Commit: tipo(escopo): descrição [SPEC-NNN T00n].") |
| DEC-5 | O `/spec-status` é necessário? | (a) criar o comando; (b) basta um `Grep` | (a), somente leitura | Rafael Santos, 2026-10-24 (chat: "Sim para o /spec-status, somente leitura.") |
| DEC-6 | Como registrar o `Entregue` depois do merge | (a) número do PR na spec dentro do próprio PR, spec `Verificada`, e o `/spec-status` mostra `Entregue` pelo estado do PR; (b) atualizar no PR da próxima spec; (c) PR mínimo de fechamento | (a). Nenhum commit depois do merge; o frontmatter guarda o `pr:`, e o `entregue-em` vem do PR | Rafael Santos, 2026-10-24 (chat: "1a") |
| DEC-7 | Um bloqueio do portão apaga o registro da sessão | (a) corrigir nesta migração, com teste; (b) deixar fora | (a) | Rafael Santos, 2026-10-24 (chat: "2a") |
| DEC-8 | Spec 004, aberta, que cita o backlog | (a) tirar o `backlog:` e citar o BL-004 como identificador anterior; (b) deixar como está | (a), sem mudar requisito, critério nem decisão | Rafael Santos, 2026-10-24 (chat: "3a") |
| DEC-9 | Número desta spec | (a) 007, porque a DEC-1 da spec 004 reserva 005 e 006; (b) 005, renumerando a série do SLA | (a) | Rafael Santos, 2026-10-24 (chat: "4a") |
| DEC-10 | Validação do formato de commit | (a) só regra no `CLAUDE.md` e nas skills; (b) também no CI | (a) | Rafael Santos, 2026-10-24 (chat: "5a") |
| DEC-11 | Alcance do REQ-7: em que estado da spec um bloqueio deixa de apagar o registro da sessão? (surgiu na T-1: `test_sdd_gate.py:147-156` protege de propósito contra reabrir a implementação só editando o status) | (a) só com a spec em `Aprovada`; (b) em qualquer estado; (c) tirar a DEC-7 da migração | (a). Resolve o caso observado em 2026-10-24 (comando bloqueado antes de a spec passar a `Em implementação`) e mantém a proteção nos demais estados; o teste da linha 147 não muda, e entra um teste novo para `Aprovada`. Refina a DEC-7 | Rafael Santos, 2026-10-24 (chat: "Opção (a). Registre como DEC-11, volte a spec para Rascunho e me mostre o diff do REQ-7, do AC-2 e da DEC-11 antes de eu aprovar de novo.") |
| DEC-12 | A regra "diff antes do commit" vale só nas skills `spec-implement` e `spec-verify`? (surgiu na T-6: fora delas, o `CLAUDE.md` exigia autorização explícita para commit, mas não o diff) | (a) estender a todo commit, dentro ou fora das skills, com uma frase na política de autonomia do `CLAUDE.md`; (b) manter só nas skills | (a). Motivo: o responsável quer revisar o diff de toda mudança antes de ela entrar no histórico, inclusive numa mudança trivial autorizada com `[sdd:autorizo]`. Entra no `CLAUDE.md` pela T-6 e é conferida pelo AC-8 |
| DEC-13 | Como tratar mudança de escopo numa spec aprovada? (surgiu depois da DEC-12: o fluxo seguido nela, registrar e seguir, contradizia a regra de voltar a spec para a descoberta) | (a) DEC nova ou ajuste de AC aprovado no chat é registrado e segue; REQ novo, REQ removido ou mudança grande de escopo volta para `Em descoberta`, com nova aprovação; (b) toda mudança volta para `Em descoberta` | (a), com a mudança grande definida por cinco critérios: a. muda o objetivo da spec; b. exige arquivos ou camadas fora do plano técnico; c. invalida uma tarefa já feita; d. muda um AC a ponto de ele deixar de cobrir o seu REQ; e. muda segurança, permissões ou o contrato público da API. Motivo: padronizar o caminho seguido na DEC-12 sem perder o controle das mudanças grandes. Entra na `spec-implement` (seção "Mudança de escopo"), no `spec-status.md` e na revisão da `spec-new`, pela T-4 reaberta, e é conferida pelo AC-5 | Rafael Santos, 2026-10-24 (chat: "Para padronizar, acrescente na skill spec-implement: DEC nova ou ajuste de AC aprovado por mim no chat é registrado na spec e segue; REQ novo, REQ removido ou mudança grande de escopo volta para descoberta e exige nova aprovação." e "Confirmo a DEC-13 com os critérios a, b, c e d, mais: e. muda segurança, permissões ou o contrato público da API. Aplique nos três arquivos e me mostre o diff antes do commit.") | Rafael Santos, 2026-10-24 (chat: "Sim, inclua a DEC-12: todo commit, dentro ou fora das skills, só depois de mostrar o diff e receber meu ok. Registre na spec com motivo e data, adicione a frase na política de autonomia do CLAUDE.md e me mostre o diff da T-6 atualizado antes do commit.") |

## 5. Fora do escopo
- Os itens abertos do backlog (BL-004 a BL-029, BL-032 a BL-034): ficam na cópia externa (DEC-1). A spec do BL-032 é posterior.
- Os falsos positivos do portão registrados no BL-033 (`git stash list`) e no BL-034 (`cd /c/...`), e o bloqueio do `git switch` entre branches com o mesmo `src/` e `tests/`: só a DEC-7 mexe no portão.
- Mudar a forma como o portão identifica a spec (DEC-3).
- Validar o formato de commit no CI (DEC-10).
- Reescrever as menções a `BL-n` em specs entregues, no `SYSTEM_SPECIFICATION.md` ou em commits antigos.
- Os arquivos `SPEC-TICKEST.md` e `specs/_fontes/gestao-acessos-onboarding.md`, por decisão do responsável.
- Código em `src/` e `tests/`: a migração não muda o sistema.

## 6. Plano técnico
- **Arquivos a alterar:**
  - `.claude/hooks/sdd_gate.py` (não apagar o registro ao bloquear) e `.claude/hooks/test_sdd_gate.py` (casos do AC-2);
  - `.claude/skills/spec-new/references/spec-status.md` (estados e transições) e `references/spec-template.md` (campos `branch:` e `pr:`, sem `backlog:`);
  - `.claude/skills/spec-new/SKILL.md` (descoberta, `[SUPOSIÇÃO]`, sem backlog);
  - `.claude/skills/spec-implement/SKILL.md` (uma tarefa por vez com commit depois do ok, mudança de escopo, DEC nova, bloqueio do portão) e `.claude/skills/spec-verify/SKILL.md` (auditoria, `pr:`, `Verificada` como estado de entrega no PR);
  - `CLAUDE.md` ("Regras permanentes": commit e branch; "Documentos de estado": sem backlog; "Fluxo de desenvolvimento (SDD)": estados e `/spec-status`);
  - `.claude/commands/auditoria-arquitetural.md` (ciclo e D4 sem backlog) e o comentário de `scripts/auditoria_sdd.py:259`;
  - `SYSTEM_SPECIFICATION.md` (6.9, 7.5, nota sobre o `BL-n` e a evidência E-39);
  - `specs/004-sla-calendario-util/spec.md` (DEC-8).
- **Arquivos a criar:** `.claude/commands/spec-status.md`, somente leitura (`Read`, `Glob`, `Grep` e `gh pr view`).
- **Arquivo a remover:** `specs/BACKLOG.md`, na última tarefa.
- **Lista de histórico do AC-1:** `specs/001-remover-responsavel/spec.md`, `specs/003-testes-arquitetura-regras-permanentes/spec.md`, `specs/004-sla-calendario-util/spec.md` (linha de evidência sobre a Q-10, linha de histórico de 2026-10-23 e a menção ao identificador anterior), `SYSTEM_SPECIFICATION.md` (C-15, Q-10, a nota nova e o registro, em 6.9 e 7.5, de que o backlog saiu do repositório), `.claude/hooks/test_sdd_gate.py` (exemplos de nome de branch) e esta spec.
- **Contratos:** nenhum.
- **Mudança no banco:** não.
- **Riscos:**
  - R-1: o portão exige o marcador `[sdd:autorizo]` na mensagem do responsável para qualquer escrita em `.claude/hooks/` e `.claude/settings*.json`, mesmo durante a `spec-implement`; a T-1 depende dessa mensagem.
  - R-2: a política de autonomia exige autorização explícita para alterar o `CLAUDE.md`, as skills e as configurações; a aprovação desta spec e o ok de cada tarefa cobrem só as mudanças listadas acima.
  - R-3: a auditoria pode perder pontos se uma mudança remover algo que ela procura (seções, regras de commit e push no `CLAUDE.md`); a T-9 roda a auditoria antes da remoção do backlog.
  - R-4: a `spec-implement` atual não faz commit; a T-4 muda isso, então as tarefas T-1 a T-3 seguem a regra atual (commit só com autorização explícita), já no formato do DEC-4.

## 7. Tarefas
Ordem por dependência; cada tarefa termina com diff, ok do responsável e commit no formato do DEC-4.

| ID | Tarefa | ACs | Skill | Feita |
|---|---|---|---|---|
| T-1 | Portão: não apagar o registro do `/spec-implement` ao bloquear; testes do AC-2 | AC-2 | — (edição direta; exige `[sdd:autorizo]`, R-1) | [x] |
| T-2 | `spec-status.md` e `spec-template.md`: ciclo da DEC-2, `Entregue` pela DEC-6, campos `branch:` e `pr:`, sem `backlog:` | AC-3 | — | [x] |
| T-3 | `spec-new`: descoberta, `[SUPOSIÇÃO]`, sem backlog | AC-4 | — | [x] |
| T-4 | `spec-implement` e `spec-verify`: disciplina de execução, mudança de escopo, DEC nova, bloqueio do portão, auditoria, `pr:` | AC-5, AC-6 | — | [x] |
| T-5 | Comando `/spec-status`, somente leitura | AC-7 | — | [x] |
| T-6 | `CLAUDE.md`: branch, commit, PR, estados, `/spec-status`, sem backlog | AC-8 | — | [x] |
| T-7 | `auditoria-arquitetural.md` e comentário do `scripts/auditoria_sdd.py` | AC-1 | — | [ ] |
| T-8 | `SYSTEM_SPECIFICATION.md` (6.9, 7.5, nota, E-39) e spec 004 (DEC-8) | AC-9, AC-14 | — | [ ] |
| T-9 | Verificação antes da remoção: auditoria, build, testes e testes do portão | AC-11, AC-12 | — | [ ] |
| T-10 | Conferir a cópia externa e remover o `specs/BACKLOG.md` (último passo); `git grep` do AC-1 | AC-1, AC-10 | — | [ ] |

## 8. Rastreabilidade
| AC | Teste(s) ou verificação | Como cobre o critério | Resultado |
|---|---|---|---|
| AC-1 | | | |
| AC-2 | `.claude/hooks/test_sdd_gate.py` › `test_spec_implement_mode_survives_a_denial_while_spec_is_still_approved` (novo), `test_spec_implement_mode_ends_when_spec_leaves_implementation` (sem alteração), `test_creating_branches_and_checking_or_staging_patches_are_allowed` (caso `git switch -c chore/SPEC-007-x`), demais testes do arquivo | Bloqueio com a spec `Aprovada` mantém o registro e libera a escrita quando ela passa a `Em implementação`; nos outros estados, o registro continua sendo apagado; criar a branch `SPEC-NNN` é liberado | 39 aprovados em 2026-10-24 (T-1; a confirmar na verificação) |
| AC-3 | Leitura de `.claude/skills/spec-new/references/spec-status.md` e `spec-template.md` (verificação descrita) | Estados `Rascunho`, `Em descoberta`, `Bloqueada`, `Aprovada`, `Em implementação`, `Verificada`, `Entregue` e `Descartada`, com as transições e os responsáveis da 4.1; `Entregue` = PR mergeado, lido do `pr:`; o modelo tem `branch:` (`<tipo>/SPEC-NNN-<slug>`) e `pr:`, sem `backlog:` | Conferido em 2026-10-24 (T-2; a confirmar na verificação) |
| AC-4 | Leitura de `.claude/skills/spec-new/SKILL.md` (verificação descrita) | Passo 2 (descoberta: leitura do contexto, não perguntar o que o código responde, no máximo 5 perguntas por rodada em negócio e técnico, parar e esperar), passo 3 (`[SUPOSIÇÃO]`), passo 5 e checklist (suposição pendente impede a aprovação); nenhuma menção a backlog | Conferido em 2026-10-24 (T-3; a confirmar na verificação) |
| AC-5 | Leitura de `.claude/skills/spec-implement/SKILL.md` (verificação descrita) | "Antes de começar" (branch e estados aceitos, recusa dos demais), "Execução" (uma tarefa por vez: implementa, build e testes, rastreabilidade, diff, ok e commit; bloqueio do portão classificado, sem contorno), "Mudança de escopo" (DEC-13: pare e pergunte; ajuste aprovado é registrado e segue; REQ novo, REQ removido ou mudança grande pelos critérios a a e volta para `Em descoberta`; DEC nova só com aprovação explícita), "Quando parar" (`Bloqueada` para os demais casos); `spec-status.md` e a revisão da `spec-new` com a mesma regra | Conferido em 2026-10-24 (T-4; a confirmar na verificação) |
| AC-6 | Leitura de `.claude/skills/spec-verify/SKILL.md` (verificação descrita) | Passo 2 (build sem avisos, `dotnet test` e `python scripts/auditoria_sdd.py .`), passo 3 (cada critério contra teste aprovado nesta execução), passo 6 (auditoria no veredito) e passo 8 (`pr:` no próprio PR) | Conferido em 2026-10-24 (T-4; a confirmar na verificação) |
| AC-7 | `.claude/commands/spec-status.md` (somente leitura: `Read`, `Glob`, `Grep` e `gh pr view`); ensaio dos passos do comando sobre `specs/*/spec.md` em 2026-10-24 | O ensaio leu o frontmatter de todas as specs (001 a 004, 007 e as specs não rastreadas do responsável) e devolveu número, título, status, branch, `aprovada-em` e `pr` iguais aos de cada arquivo; a execução do `/spec-status` em si fica para a verificação | Ensaio conferido em 2026-10-24 (T-5; o `/spec-status` roda na verificação) |
| AC-8 | Leitura do `CLAUDE.md` ("Regras permanentes", "Documentos de estado", "Fluxo de desenvolvimento (SDD)" e "Portão determinístico") e `grep -i -E "backlog\|BL-[0-9]" CLAUDE.md` sem resultado | Branch `<tipo>/SPEC-NNN-<slug>` com os sete tipos; commit `tipo(escopo): descrição [SPEC-NNN T00n]`; PR com `SPEC-NNN`, link do `spec.md` e REQs; spec como unidade de trabalho, `/spec-status`, descoberta, uma tarefa por vez e entrega pelo merge; política de autonomia com "diff e ok antes de todo commit" (DEC-12) | Conferido em 2026-10-24 (T-6; a confirmar na verificação) |
| AC-9 | | | |
| AC-10 | | | |
| AC-11 | | | |
| AC-12 | | | |
| AC-13 | | | |
| AC-14 | | | |

## 9. Evidências de verificação
- **Build:**
- **Testes:**
- **Revisão:**
- **Documentação atualizada:** a atualizar em `SYSTEM_SPECIFICATION.md`: 6.9, 7.5, a nota sobre o `BL-n` e a evidência E-39.
- **CI:**
- **Entrega:**

## 10. Histórico
| Data | Status | Observação |
|---|---|---|
| 2026-10-24 | Rascunho | Spec criada pela `spec-new` a partir do `prompt-migracao-spec-first.md`, depois do inventário (Fase A) e de duas rodadas de descoberta com Rafael Santos no chat; DEC-1 a DEC-10 decididas, sem `[SUPOSIÇÃO]` pendente. Checklist de completude conferido. Escrita pelas regras atuais da `spec-new` (estado `Rascunho`, campo `backlog:` vazio); o ciclo novo vale depois da T-2. Aguardando aprovação. |
| 2026-10-24 | Aprovada | Aprovação explícita de Rafael Santos no chat ("Aprovo a spec 007"). Checklist de completude conferido; nenhuma decisão em aberto e nenhum `[SUPOSIÇÃO]`. Pronta para `/spec-implement 007`, na branch `chore/SPEC-007-spec-first-sem-backlog`. |
| 2026-10-24 | Em implementação | Início da implementação pela `spec-implement` (`/spec-implement 007`, Rafael Santos no chat), na branch `chore/SPEC-007-spec-first-sem-backlog`. |
| 2026-10-24 | Bloqueada | Origem: `Em implementação`, antes de qualquer alteração da T-1. A spec se contradiz: o REQ-7 pede que um bloqueio não apague o registro do `/spec-implement`, mas o AC-2 exige que os testes atuais passem, e `test_sdd_gate.py:147-156` (`test_spec_implement_mode_ends_when_spec_leaves_implementation`) afirma de propósito que, depois de a spec sair de `Em implementação` (`Bloqueada`, `Verificada` ou `Rascunho`) e uma escrita ser bloqueada, a sessão só volta a escrever com um novo `/spec-implement`. Pendência: decisão do responsável sobre o alcance do REQ-7. |
| 2026-10-24 | Rascunho | Pendência resolvida pela DEC-11 (opção (a), Rafael Santos no chat). Como a resolução altera o REQ-7 e o AC-2, a spec volta para `Rascunho` pela `spec-new`: REQ-7 restrito ao estado `Aprovada`; AC-2 explicita que o teste da linha 147 não muda. Demais REQs, ACs, DECs e tarefas sem mudança; nenhuma tarefa feita. `aprovada-em` esvaziado: precisa de nova aprovação. Checklist de completude conferido. |
| 2026-10-24 | Aprovada | Nova aprovação explícita de Rafael Santos no chat ("Aprovo a spec 007"), com a DEC-11. Pronta para `/spec-implement 007`. |
| 2026-10-24 | Em implementação | Retomada da implementação pela `spec-implement` (`/spec-implement 007`, Rafael Santos no chat), a partir da T-1. |
| 2026-10-24 | Em implementação | Durante a T-6, DEC-12 nova, aprovada explicitamente por Rafael Santos no chat, com motivo e data: todo commit, dentro ou fora das skills, só depois de mostrar o diff e receber o ok do responsável. O AC-8 passa a conferir essa frase na política de autonomia do `CLAUDE.md`, por pedido do responsável na mesma mensagem; nenhum outro REQ, AC ou DEC mudou. |
| 2026-10-24 | Em implementação | DEC-13 nova, aprovada explicitamente por Rafael Santos no chat, com motivo e data (mudança de escopo: ajuste aprovado é registrado e segue; REQ novo, REQ removido ou mudança grande, pelos critérios a a e, volta para `Em descoberta`). A T-4 é reaberta para aplicá-la na `spec-implement`, no `spec-status.md` e na revisão da `spec-new`, num commit `[SPEC-007 T004]`; o AC-5 passa a conferir a regra, a pedido do responsável. Nenhum outro REQ, AC ou DEC mudou. |
