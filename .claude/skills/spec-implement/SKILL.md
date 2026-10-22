---
name: spec-implement
description: Implementa uma spec aprovada, tarefa por tarefa, usando as skills de implementação do projeto e preenchendo a rastreabilidade entre critérios de aceitação e testes. Só roda quando chamada explicitamente com /spec-implement.
argument-hint: <número ou pasta da spec, ex.: "001" ou "specs/001-remover-responsavel">
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
2. Confira o `status`:
   - `Aprovada`: mude para `Em implementação` e registre a linha no histórico;
   - `Em implementação`: é uma retomada; continue da primeira tarefa não concluída;
   - `Verificada`: só continue se houver alteração de código a fazer; volte para `Em implementação`, registre o motivo no histórico e avise que será preciso verificar de novo;
   - `Bloqueada`: siga "Retomada de uma spec bloqueada";
   - qualquer outro estado: **recuse**, informe o status atual e o que falta.
3. Leia a spec inteira e o contexto do projeto.

## Retomada de uma spec bloqueada

1. Leia no histórico o estado de origem e a pendência que bloqueou a spec. Se a origem foi `Rascunho`, a retomada cabe à skill `spec-new`: pare e informe.
2. Confirme com o responsável que a pendência foi resolvida e registre a resolução: uma decisão nova ou atualizada na tabela de decisões (com quem decidiu e quando) ou uma observação no histórico.
3. Se a resolução alterar requisito, critério ou decisão já aprovados, a spec volta para `Rascunho` e precisa de nova aprovação: pare e indique a skill `spec-new`.
4. Caso contrário, volte para `Em implementação`, registrando a resolução no histórico. Preserve as tarefas já marcadas, a rastreabilidade e o histórico, e continue da primeira tarefa não concluída.
5. Enquanto a pendência não estiver resolvida, a spec continua `Bloqueada`: não implemente nada.

## Execução

1. Execute as tarefas na ordem da spec. Para cada uma, use a skill indicada na coluna "Skill" (por exemplo, as de criar entidade, slice e testes) e siga o contexto do projeto; não repita aqui as convenções delas.
2. Ao concluir uma tarefa, marque `[x]` e preencha, na tabela de rastreabilidade, os testes reais (arquivo › método) que cobrem os ACs dela e, em uma linha, como cada teste cobre o critério.
3. Rode o build e os testes do projeto ao fim de cada tarefa que altere código e corrija antes de seguir.

## Quando parar

Pare, mude o status para `Bloqueada`, registre no histórico o estado de origem e o motivo, e pergunte ao responsável quando:
- surgir uma decisão que a spec não cobre;
- for preciso alterar algo fora do plano técnico;
- um AC se mostrar impossível de verificar;
- a spec contradisser o `CLAUDE.md`;
- um teste falhar por um defeito fora do escopo da spec.

## Ao terminar

1. Confira que todas as tarefas estão marcadas e que todo AC tem pelo menos um teste na tabela.
2. Informe o que foi feito e indique a skill `spec-verify` como próximo passo. O status continua `Em implementação` até a verificação.

## Regras

- Não faça stage, commit nem push.
- Não altere requisitos, critérios nem decisões aprovados: mudança neles volta a spec para `Rascunho` e exige nova aprovação.
- Não implemente nada que não esteja numa tarefa da spec.
