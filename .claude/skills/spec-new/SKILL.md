---
name: spec-new
description: Cria ou revisa a especificação (spec) de uma mudança funcional antes de qualquer código — faz a descoberta com perguntas de negócio e técnicas, escreve requisitos, critérios de aceitação verificáveis, decisões, plano e tarefas, e para à espera da aprovação do responsável. Use quando o usuário pedir uma funcionalidade nova, uma mudança de comportamento ou uma correção não trivial.
argument-hint: '<pedido, ex.: "permitir remover o responsável de um chamado">'
---

# Criar a spec de uma mudança

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Esta skill só escreve dentro de `specs/`. Ela nunca altera código, testes, configurações nem a documentação do projeto. Os estados e as transições da spec seguem [references/spec-status.md](references/spec-status.md). A spec é a unidade de trabalho: todo trabalho começa numa spec, e o estado de cada demanda fica só no frontmatter dela. As ideias e as prioridades ficam com o responsável, fora do repositório.

## Fluxo

1. **Registre o pedido** com as palavras do solicitante e crie a pasta `specs/NNN-slug/`, com o próximo número livre (respeitando números já reservados em decisões de outras specs) e o slug em minúsculas com hífens. A spec nasce em `Rascunho` e passa a `Em descoberta` assim que a descoberta começa, com a linha no histórico.
2. **Descubra antes de escrever:**
   1. Leia o pedido, o código afetado (slices, entidades, testes), as specs relacionadas, o `CLAUDE.md` (contexto do projeto e política de autonomia) e as seções relevantes do documento de estado do sistema.
   2. Não pergunte o que dá para descobrir no código ou nos documentos: descubra e informe, citando arquivo e linha.
   3. Faça no máximo **5 perguntas por rodada**, numeradas e separadas em:
      - **Negócio:** objetivo, usuários, regras, exceções e o que fica fora do escopo;
      - **Técnico:** para cada decisão, 2 a 3 opções com prós e contras, a sua recomendação e o motivo, considerando a arquitetura atual (Clean Architecture, Result pattern, CQRS, eventos de domínio, auditoria, exclusão lógica e as demais convenções do `CLAUDE.md`).
   4. **Pare e espere as respostas.** Repita as rodadas até não restar dúvida relevante.
3. **Escreva a spec** a partir de [references/spec-template.md](references/spec-template.md):
   - requisitos (REQ-n), cada um com a sua fonte;
   - critérios de aceitação (AC-n) verificáveis, no formato dado/quando/então, com resultado observável (status HTTP, estado gravado, registro produzido);
   - requisitos não funcionais, quando forem relevantes;
   - cada resposta da descoberta vira uma decisão (DEC-n), com as opções, quem decidiu e quando;
   - toda suposição que o responsável não confirmou fica marcada como **`[SUPOSIÇÃO]`** no ponto em que aparece; nunca vira requisito sem marcação;
   - o que fica fora do escopo, explícito;
   - riscos, plano técnico e tarefas (T-n), quando houver, cada tarefa ligada a pelo menos um AC e à skill que a executará;
   - a branch no campo `branch:`, no padrão `<tipo>/SPEC-NNN-<slug>` do `CLAUDE.md`, com o tipo conforme a natureza da mudança.
4. **Confira a completude** com o checklist abaixo. Se faltar algo, a spec continua `Em descoberta`, ou passa para `Bloqueada` se depender de uma decisão externa, com a linha correspondente no histórico.
5. **Peça a aprovação** e pare. Só marque `status: Aprovada`, preenchendo `responsavel` e `aprovada-em` e acrescentando a linha no histórico, quando o responsável aprovar explicitamente no chat. Uma spec com `[SUPOSIÇÃO]` pendente não pode ser aprovada: pergunte antes. A aprovação de um plano anterior ou de outra spec não vale.

## Revisão de uma spec existente

1. Numa spec `Aprovada`, `Em implementação` ou `Bloqueada`: se a revisão trouxer REQ novo, REQ removido ou mudança grande de escopo (critérios a a e na seção "Mudança de escopo" da `spec-implement`), registre no histórico a transição para `Em descoberta`, com o motivo e quem pediu; se for só DEC nova ou ajuste de AC aprovados explicitamente pelo responsável no chat, registre na spec com o motivo, a data e as palavras dele, sem mudar o estado nem pedir nova aprovação. Uma spec `Verificada` passa antes por `Em implementação`, pela `spec-implement`, como manda a tabela de transições. Uma spec antiga em `Rascunho` passa a `Em descoberta` quando a revisão começar.
2. Faça a descoberta da mudança (passo 2 do fluxo) e altere preservando o histórico, as tarefas feitas e a rastreabilidade; registre no histórico o que mudou.
3. Rode de novo o checklist de completude e peça nova aprovação explícita.

## Checklist de completude

- [ ] Nenhuma pergunta da descoberta ficou sem resposta.
- [ ] Nenhum `[SUPOSIÇÃO]` pendente.
- [ ] Todo REQ tem pelo menos um AC.
- [ ] Todo AC pode ser verificado por um teste automatizado ou por uma verificação descrita.
- [ ] Nenhuma DEC em aberto, ou as restantes estão em "Fora do escopo".
- [ ] Toda tarefa aponta para ACs, e todo AC é coberto por alguma tarefa.
- [ ] O plano segue os caminhos e os desvios declarados no `CLAUDE.md` do projeto.
- [ ] O impacto na documentação de estado do sistema está identificado.

## Regras

- Não escreva código nem testes, nem como rascunho fora de `specs/`.
- Não invente requisitos para preencher o modelo: seção sem conteúdo fica com "Não se aplica".
- Não escreva a spec antes de terminar a descoberta, nem decida sozinho o que o responsável não confirmou: pergunte ou marque `[SUPOSIÇÃO]`.
- Spec aprovada com REQ novo, REQ removido ou mudança grande de escopo volta para `Em descoberta` e exige nova aprovação; DEC nova ou ajuste de AC aprovados no chat são registrados e seguem.
- Spec `Bloqueada` durante a escrita volta para `Em descoberta` quando a pendência for resolvida; registre a resolução no histórico.
