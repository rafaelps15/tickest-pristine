# Estados e transições da spec

Fonte única dos estados de uma spec. O modelo (`spec-template.md`) e as skills `spec-new`, `spec-implement` e `spec-verify` seguem esta tabela; nenhuma outra transição é permitida. A spec é a unidade de trabalho: o estado de cada demanda fica só no frontmatter da spec dela.

## Estados

| Estado | Significado |
|---|---|
| `Rascunho` | Criada; a descoberta ainda não começou. |
| `Em descoberta` | A `spec-new` está fazendo as perguntas e escrevendo a spec; ainda não pode ser implementada. |
| `Bloqueada` | Parada por uma pendência que depende de decisão ou informação externa. |
| `Aprovada` | Aprovada explicitamente pelo responsável; pronta para implementar. |
| `Em implementação` | Tarefas em execução, ou verificação ainda não aprovada. |
| `Verificada` | Todos os critérios de aceitação comprovados por testes aprovados na execução atual; o PR leva a spec neste estado, com o campo `pr:` preenchido. |
| `Entregue` | PR mergeado na `main`. Final. |
| `Descartada` | Abandonada por decisão explícita do responsável. Final. |

## Transições permitidas

| De | Para | Condição | Quem registra |
|---|---|---|---|
| `Rascunho` | `Em descoberta` | `/spec-new` iniciado | `spec-new` (agente) |
| `Em descoberta` | `Aprovada` | Todas as perguntas respondidas, nenhum `[SUPOSIÇÃO]` pendente, checklist de completude atendido e aprovação explícita do responsável no chat; registra `aprovada-em` | O responsável aprova; a `spec-new` registra |
| `Em descoberta` | `Bloqueada` | A escrita depende de decisão ou informação externa | `spec-new` |
| `Aprovada` | `Em implementação` | Branch criada e `/spec-implement` iniciado | `spec-implement` |
| `Aprovada` | `Em descoberta` | Requisito, critério ou decisão aprovados precisam mudar | `spec-new` |
| `Em implementação` | `Bloqueada` | Uma condição de parada da implementação | `spec-implement` |
| `Em implementação` | `Em descoberta` | A implementação revelou que requisito, critério ou decisão aprovados precisam mudar | `spec-new` |
| `Em implementação` | `Verificada` | `spec-verify` com todos os critérios de aceitação cobertos por testes aprovados na execução atual | `spec-verify` |
| `Bloqueada` | estado de origem (`Em descoberta` ou `Em implementação`) | Pendência resolvida e registrada, sem alterar requisito, critério ou decisão aprovados | `spec-new` (origem `Em descoberta`) ou `spec-implement` (origem `Em implementação`) |
| `Bloqueada` | `Em descoberta` | A resolução da pendência alterou requisito, critério ou decisão aprovados | `spec-new` |
| `Verificada` | `Em implementação` | Código alterado depois da verificação, pela `spec-implement` ou fora dela (outra skill, edição à mão ou mudança trivial autorizada); exige nova verificação | `spec-implement`, ou quem alterou o código fora dela |
| `Verificada` | `Entregue` | O responsável mergeou o PR da spec na `main` | Ninguém grava: o `/spec-status` mostra `Entregue`, com a data do merge (`entregue-em`), a partir do estado do PR do campo `pr:` |
| qualquer estado não final | `Descartada` | Decisão explícita do responsável | Quem recebeu a decisão |

## Regras

- Não pule estados nem mude o status por inferência: só as transições acima, com a condição atendida.
- Toda transição gravada acrescenta uma linha na seção "Histórico" da spec: data, novo status e observação com o motivo e, quando houver, quem decidiu. Linhas anteriores nunca são apagadas nem editadas.
- Ao entrar em `Bloqueada`, a observação registra o estado de origem e a pendência; ao sair, registra como a pendência foi resolvida.
- Tarefas marcadas como feitas, rastreabilidade e evidências já registradas são preservadas em qualquer transição; só são revistas se a mudança as invalidar, com a revisão anotada no histórico.
- O número do PR entra no campo `pr:` e na seção "Evidências de verificação" num commit do próprio PR, com a spec em `Verificada`. Depois do merge, nenhum commit é necessário: `Entregue` é lido do estado do PR. O frontmatter de uma spec entregue continua dizendo `Verificada`.
- Specs entregues antes da spec 007 têm `status: Entregue` no frontmatter, pela regra anterior (commit ou PR autorizado); o `/spec-status` mostra esse valor como está.
- `Entregue` e `Descartada` são finais. Uma spec descartada continua no repositório, com o motivo no histórico. Specs anteriores à spec 007 que estejam em `Rascunho` passam a `Em descoberta` quando a `spec-new` retomar a escrita.
- Quando a `spec-implement` ou a `spec-verify` encontrarem algo que exija mudar requisito, critério ou decisão aprovados, elas não fazem a transição para `Em descoberta`: param, informam e indicam a `spec-new`, que registra a transição.
- Uma linha de histórico pode repetir o status atual para registrar uma revisão sem transição.
- Código coberto por uma spec `Verificada` alterado depois da verificação, por qualquer skill ou à mão, invalida a verificação: antes do merge, a spec volta para `Em implementação` e passa de novo pela `spec-verify`.
