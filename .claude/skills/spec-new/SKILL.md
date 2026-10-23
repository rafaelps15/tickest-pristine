---
name: spec-new
description: Cria ou revisa a especificação (spec) de uma mudança funcional antes de qualquer código — investiga o contexto, escreve requisitos, critérios de aceitação verificáveis, decisões em aberto, plano e tarefas, e para à espera da aprovação do responsável. Use quando o usuário pedir uma funcionalidade nova, uma mudança de comportamento ou uma correção não trivial.
argument-hint: '<pedido, ex.: "permitir remover o responsável de um chamado">'
---

# Criar a spec de uma mudança

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Esta skill só escreve dentro de `specs/`. Ela nunca altera código, testes, configurações nem a documentação do projeto. Os estados e as transições da spec seguem [references/spec-status.md](references/spec-status.md).

## Fluxo

1. **Registre o pedido** com as palavras do solicitante.
2. **Leia o contexto:** o `CLAUDE.md` (contexto do projeto e política de autonomia) e só as seções relevantes do documento de estado do sistema indicado nele. Localize no código os slices, entidades e testes afetados.
3. **Crie a pasta** `specs/NNN-slug/`, com o próximo número livre e o slug em minúsculas com hífens.
4. **Escreva a spec** a partir de [references/spec-template.md](references/spec-template.md):
   - requisitos (REQ-n), cada um com a sua fonte;
   - critérios de aceitação (AC-n) verificáveis, no formato dado/quando/então, com resultado observável (status HTTP, estado gravado, registro produzido);
   - toda regra sem fonte no pedido, no código ou nos documentos vira decisão em aberto (DEC-n), com as opções; nunca uma suposição;
   - o que fica fora do escopo, explícito;
   - plano técnico e tarefas (T-n), cada tarefa ligada a pelo menos um AC e à skill que a executará.
5. **Pergunte as decisões em aberto** de forma objetiva, com as opções e a sua recomendação. Registre a resposta na spec, com quem decidiu e quando.
6. **Confira a completude** com o checklist abaixo. Se faltar algo, a spec fica em `Rascunho`, ou passa para `Bloqueada` se depender de uma decisão externa, com a linha correspondente no histórico.
7. **Peça a aprovação** e pare. Só marque `status: Aprovada`, preenchendo `responsavel` e `aprovada-em` e acrescentando a linha no histórico, quando o responsável aprovar explicitamente no chat. A aprovação de um plano anterior ou de outra spec não vale.

## Revisão de uma spec existente

1. Se a revisão mudar requisito, critério ou decisão de uma spec `Aprovada`, `Em implementação` ou `Bloqueada`, registre no histórico a transição para `Rascunho`, com o motivo e quem pediu. Uma spec `Verificada` passa antes por `Em implementação`, pela `spec-implement`, como manda a tabela de transições.
2. Altere preservando o histórico, as tarefas feitas e a rastreabilidade; registre no histórico o que mudou.
3. Rode de novo o checklist de completude e peça nova aprovação explícita.

## Checklist de completude

- [ ] Todo REQ tem pelo menos um AC.
- [ ] Todo AC pode ser verificado por um teste automatizado ou por uma verificação descrita.
- [ ] Nenhuma DEC em aberto, ou as restantes estão em "Fora do escopo".
- [ ] Toda tarefa aponta para ACs, e todo AC é coberto por alguma tarefa.
- [ ] O plano segue os caminhos e os desvios declarados no `CLAUDE.md` do projeto.
- [ ] O impacto na documentação de estado do sistema está identificado.

## Regras

- Não escreva código nem testes, nem como rascunho fora de `specs/`.
- Não invente requisitos para preencher o modelo: seção sem conteúdo fica com "Não se aplica".
- Spec aprovada que precise mudar volta para `Rascunho` e exige nova aprovação.
- Spec `Bloqueada` durante a escrita volta para `Rascunho` quando a pendência for resolvida; registre a resolução no histórico.
