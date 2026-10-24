---
description: Mostra o estado de todas as specs (somente leitura), a partir do frontmatter de cada spec.md e do estado do PR
allowed-tools: Read, Glob, Grep, Bash(gh pr view:*)
---

# Estado das specs

Comando **somente leitura**: não crie, edite, mova nem apague arquivo nenhum, e não rode comandos que mudem o repositório. Ele substitui a visão geral que um arquivo central daria: o estado de cada demanda fica só no frontmatter da spec dela (`.claude/skills/spec-new/references/spec-status.md`).

## Passos

1. Liste com `Glob` os arquivos `specs/*/spec.md`, ignorando as pastas que começam com `_` (fontes e rascunhos).
2. Em cada um, leia só o frontmatter (o bloco entre as duas linhas `---` do topo) e anote `id`, `titulo`, `status` (sem o comentário depois de `#`), `branch`, `aprovada-em` e `pr`. Campo ausente ou vazio vira `—`. Arquivo sem frontmatter aparece com o nome da pasta e o status `sem frontmatter`.
3. Para cada spec com `pr` preenchido, rode `gh pr view <número> --json state,mergedAt,url`:
   - `state` igual a `MERGED`: mostre o status como `Entregue` e o `entregue-em` como a data de `mergedAt` (`AAAA-MM-DD`);
   - outro estado: mostre o status do frontmatter e, no PR, o estado (`OPEN` ou `CLOSED`);
   - se o `gh` falhar (sem rede ou sem login), mostre o status do frontmatter e marque o PR com `não consultado`, dizendo o motivo uma única vez abaixo da tabela.
4. Sem `pr`, o `entregue-em` fica `—`, e o status é o do frontmatter (specs entregues antes da spec 007 têm `status: Entregue` no próprio frontmatter).

## Saída

Uma tabela só, ordenada pelo número da spec:

| Nº | Título | Status | Branch | Aprovada em | PR | Entregue em |
|---|---|---|---|---|---|---|

Abaixo dela, só quando houver: as specs `sem frontmatter` e o aviso de PR não consultado. Não acrescente análise nem recomendação.
