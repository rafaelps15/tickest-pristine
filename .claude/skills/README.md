# Skills do TickestPristine para o Claude Code

Um pacote de skills que ensina ao Claude Code as convenções do TickestPristine — para que cada feature que ele construir pareça escrita por você: casos de uso em slices verticais, handlers próprios de command/query (sem MediatR), tratamento de erro com Result, endpoints de minimal API e cobertura completa de testes.

## O que tem dentro

| Skill | Como chamar | O que faz |
|---|---|---|
| **add-feature** | `/add-feature archive a todo item` | Cria um slice vertical completo: command/query, handler, validator, endpoint e testes unitários, de validator e de integração. |
| **add-entity** | `/add-entity Project with a name and owner` | Adiciona uma entidade de domínio de ponta a ponta: entidade, catálogo de erros, eventos de domínio, configuração do EF, ligação no DbContext e migration. |
| **add-tests** | `/add-tests CopyTodoCommand` | Completa os testes de handler, de validator e de integração de casos de uso existentes. |
| **ca-review** | `/ca-review` | Revisa as mudanças pendentes contra as convenções do TickestPristine: limites entre camadas, tratamento de erro, segurança, cache e cobertura de testes. |
| **spec-new** | `/spec-new permitir remover o responsável de um chamado` | Escreve a spec de uma mudança (requisitos, critérios de aceitação, decisões, plano e tarefas) e para à espera da aprovação. |
| **spec-implement** | `/spec-implement 001` | Implementa uma spec aprovada, tarefa por tarefa, com as skills acima. Só roda pelo comando. |
| **spec-verify** | `/spec-verify 001` | Comprova cada critério de aceitação com testes aprovados na execução atual e atualiza a documentação de estado. |

Não é preciso chamá-las explicitamente — depois de instaladas, o Claude Code escolhe a skill certa sozinho quando você diz algo como "add an endpoint to snooze a todo".

> Neste repositório, as skills de código só escrevem durante o `/spec-implement` de uma spec aprovada com status `Em implementação`, ou numa mudança trivial autorizada com o marcador `[sdd:autorizo]` na mensagem do responsável; um hook de projeto bloqueia escrita em `src/` e `tests/` fora desses dois casos.

## Instalação

As skills ficam em `.claude/skills/`. Se você clonou o TickestPristine, elas já estão ativas — basta abrir o repositório no Claude Code.

Para usá-las em outro projeto baseado no TickestPristine, copie a pasta:

```
your-project/
└── .claude/
    └── skills/
        ├── add-feature/
        ├── add-entity/
        ├── add-tests/
        ├── ca-review/
        ├── spec-new/
        ├── spec-implement/
        └── spec-verify/
```

## Experimente

```
/add-feature snooze a todo until a given date
```

O Claude vai criar o command, o validator, o handler (com conferência de dono, evento de domínio e invalidação de cache (quando o projeto usa cache nas leituras)), o endpoint e os três tipos de teste — e depois rodar o build e os testes.

## Personalizando

Cada skill é um arquivo Markdown simples (`SKILL.md`, mais os modelos em `references/`). Mudou o nome das camadas, prefere records em todo lugar, usa outra pilha de testes? Edite os modelos uma vez e todas as próximas features seguem o mesmo padrão. As skills são a versão executável do documento de convenções do seu time.
