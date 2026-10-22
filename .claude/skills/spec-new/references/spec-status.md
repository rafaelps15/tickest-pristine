# Estados e transições da spec

Fonte única dos estados de uma spec. O modelo (`spec-template.md`) e as skills `spec-new`, `spec-implement` e `spec-verify` seguem esta tabela; nenhuma outra transição é permitida.

## Estados

| Estado | Significado |
|---|---|
| `Rascunho` | Em escrita ou em revisão; ainda não pode ser implementada. |
| `Bloqueada` | Parada por uma pendência que depende de decisão ou informação externa. |
| `Aprovada` | Aprovada explicitamente pelo responsável; pronta para implementar. |
| `Em implementação` | Tarefas em execução, ou verificação ainda não aprovada. |
| `Verificada` | Todos os critérios de aceitação comprovados por testes aprovados na execução atual; ainda **não** entregue. |
| `Entregue` | Commit ou PR feitos com autorização explícita do responsável. Final. |
| `Cancelada` | Abandonada por decisão explícita do responsável. Final. |

## Transições permitidas

| De | Para | Condição | Quem registra |
|---|---|---|---|
| `Rascunho` | `Aprovada` | Checklist de completude atendido e aprovação explícita do responsável no chat | `spec-new` |
| `Rascunho` | `Bloqueada` | A escrita depende de decisão ou informação externa | `spec-new` |
| `Aprovada` | `Em implementação` | Início da implementação | `spec-implement` |
| `Aprovada` | `Rascunho` | Requisito, critério ou decisão aprovados precisam mudar | `spec-new` |
| `Em implementação` | `Bloqueada` | Uma condição de parada da implementação | `spec-implement` |
| `Em implementação` | `Rascunho` | A implementação revelou que requisito, critério ou decisão aprovados precisam mudar | `spec-new` |
| `Em implementação` | `Verificada` | Verificação aprovada | `spec-verify` |
| `Bloqueada` | estado de origem (`Rascunho` ou `Em implementação`) | Pendência resolvida e registrada, sem alterar requisito, critério ou decisão aprovados | `spec-new` (origem `Rascunho`) ou `spec-implement` (origem `Em implementação`) |
| `Bloqueada` | `Rascunho` | A resolução da pendência alterou requisito, critério ou decisão aprovados | `spec-new` |
| `Verificada` | `Em implementação` | Código alterado depois da verificação; exige nova verificação | `spec-implement` |
| `Verificada` | `Entregue` | Commit ou PR feitos com autorização explícita do responsável | Quem executou a entrega autorizada |
| qualquer estado não final | `Cancelada` | Decisão explícita do responsável | Quem recebeu a decisão |

## Regras

- Não pule estados nem mude o status por inferência: só as transições acima, com a condição atendida.
- Toda transição acrescenta uma linha na seção "Histórico" da spec: data, novo status e observação com o motivo e, quando houver, quem decidiu. Linhas anteriores nunca são apagadas nem editadas.
- Ao entrar em `Bloqueada`, a observação registra o estado de origem e a pendência; ao sair, registra como a pendência foi resolvida.
- Tarefas marcadas como feitas, rastreabilidade e evidências já registradas são preservadas em qualquer transição; só são revistas se a mudança as invalidar, com a revisão anotada no histórico.
- `Verificada` não é `Entregue`. A entrega depende de autorização explícita e registra o commit ou o PR na seção "Evidências de verificação" da spec.
- `Entregue` e `Cancelada` são finais. Uma spec cancelada continua no repositório, com o motivo no histórico.
