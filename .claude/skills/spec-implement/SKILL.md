---
name: spec-implement
description: Implementa uma spec aprovada, uma tarefa por vez (implementa, roda build e testes, mostra o diff, espera o ok e commita), usando as skills de implementação do projeto e preenchendo a rastreabilidade entre critérios de aceitação e testes. Só roda quando chamada explicitamente com /spec-implement.
argument-hint: '<número ou pasta da spec, ex.: "001" ou "specs/001-remover-responsavel">'
disable-model-invocation: true
---

# Implementar uma spec aprovada

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Os estados e as transições da spec seguem [../spec-new/references/spec-status.md](../spec-new/references/spec-status.md).

## Antes de começar

1. Localize a spec indicada em `specs/`. Se ela não existir, pare e informe.
2. Confira a branch: ela segue o campo `branch:` da spec (`<tipo>/SPEC-NNN-<slug>`), criada a partir da `origin/main`. Se não estiver nela, pare e informe o comando para o responsável criar ou trocar de branch.
3. Confira o `status`:
   - `Aprovada`: mude para `Em implementação` e registre a linha no histórico;
   - `Em implementação`: é uma retomada; continue da primeira tarefa não concluída;
   - `Verificada`: só continue se houver alteração de código a fazer; volte para `Em implementação`, registre o motivo no histórico e avise que será preciso verificar de novo;
   - `Bloqueada`: siga "Retomada de uma spec bloqueada";
   - qualquer outro estado (`Rascunho`, `Em descoberta`, `Entregue`, `Descartada`): **recuse**, informe o status atual e o que falta.
4. Leia a spec inteira e o contexto do projeto.

## Retomada de uma spec bloqueada

1. Leia no histórico o estado de origem e a pendência que bloqueou a spec. Se a origem foi `Em descoberta` (ou `Rascunho`, em specs anteriores à spec 007), a retomada cabe à skill `spec-new`: pare e informe.
2. Confirme com o responsável que a pendência foi resolvida e registre a resolução: uma decisão nova ou atualizada na tabela de decisões (com quem decidiu, quando e o motivo) ou uma observação no histórico.
3. Se a resolução alterar requisito, critério ou decisão já aprovados, a spec volta para `Em descoberta` e precisa de nova aprovação: pare e indique a skill `spec-new`.
4. Caso contrário, volte para `Em implementação`, registrando a resolução no histórico. Preserve as tarefas já marcadas, a rastreabilidade e o histórico, e continue da primeira tarefa não concluída.
5. Enquanto a pendência não estiver resolvida, a spec continua `Bloqueada`: não implemente nada.

## Execução

Execute **uma tarefa por vez**, na ordem da spec. Para cada tarefa:

1. **Implemente** usando a skill indicada na coluna "Skill" (por exemplo, as de criar entidade, slice e testes) e siga o contexto do projeto; não repita aqui as convenções delas.
2. **Rode o build e os testes** do projeto (quando a tarefa alterar código) e corrija antes de seguir.
3. **Marque `[x]`** e preencha, na tabela de rastreabilidade, os testes reais (arquivo › método) que cobrem os ACs da tarefa e, em uma linha, como cada teste cobre o critério.
4. **Mostre o diff** da tarefa e o resultado do build e dos testes, com o commit proposto no formato `tipo(escopo): descrição [SPEC-NNN T00n]`.
5. **Espere o ok** do responsável. Só então faça o stage dos arquivos da tarefa e o commit, com o trailer de coautoria do agente. Só depois passe para a próxima tarefa.

Se o portão SDD bloquear um comando, não o contorne: classifique o bloqueio como **correto** (a escrita violaria a regra) ou **falso positivo** (a escrita não toca `src/`, `tests/` nem o portão) e informe o responsável. Quando o comando for necessário, passe a ele o comando exato para rodar com `!`.

## Quando parar

Pare, mude o status para `Bloqueada`, registre no histórico o estado de origem e o motivo, e pergunte ao responsável quando:
- for preciso incluir, remover ou alterar um REQ, um critério de aceitação ou uma DEC da spec aprovada (mudança de escopo);
- surgir uma decisão que a spec não cobre: nenhuma DEC nova entra sem aprovação explícita do responsável no chat, registrada na spec com o motivo e a data;
- for preciso alterar algo fora do plano técnico;
- um AC se mostrar impossível de verificar;
- a spec contradisser o `CLAUDE.md`;
- um teste falhar por um defeito fora do escopo da spec.

## Ao terminar

1. Confira que todas as tarefas estão marcadas e que todo AC tem pelo menos um teste na tabela.
2. Informe o que foi feito e indique a skill `spec-verify` como próximo passo. O status continua `Em implementação` até a verificação.

## Regras

- Commit só depois do ok do responsável para aquela tarefa, só com os arquivos dela. Nunca faça push, rebase, merge nem `git rm` sem autorização; quando o portão exigir, o responsável roda esses comandos com `!`.
- Não altere requisitos, critérios nem decisões aprovados: mudança neles volta a spec para `Em descoberta` e exige nova aprovação.
- Não implemente nada que não esteja numa tarefa da spec.
