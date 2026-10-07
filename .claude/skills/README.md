# Skills de Clean Architecture para o Claude Code

Um pacote de skills que ensina ao Claude Code as convenções do template de Clean Architecture — para que toda feature que ele criar pareça escrita por você: casos de uso em slices verticais, handlers próprios de command/query (sem MediatR), tratamento de erro com `Result`, endpoints de minimal API e cobertura completa de testes.

## O que tem aqui

| Skill | Como chamar | O que faz |
|---|---|---|
| **add-feature** | `/add-feature arquivar uma tarefa` | Cria um slice vertical completo: command/query, handler, validator, endpoint e testes unitários, de validator e de integração. |
| **add-entity** | `/add-entity TodoItem com descrição e dono` | Adiciona uma entidade de domínio de ponta a ponta: entidade, catálogo de erros, eventos de domínio, configuração do EF, ligação no DbContext e migration. |
| **add-tests** | `/add-tests CopyTodoCommand` | Completa os testes de handler, de validator e de integração de casos de uso existentes. |
| **ca-review** | `/ca-review` | Revisa as mudanças pendentes contra as convenções do template: limites entre camadas, tratamento de erro, segurança, cache e cobertura de testes. |

Não é preciso chamá-las explicitamente — depois de instaladas, o Claude Code escolhe a skill certa sozinho quando você diz algo como "adicione um endpoint para adiar uma tarefa".

## Instalação

As skills ficam em `.claude/skills/`. Se você clonou o template, elas já estão ativas — basta abrir o repositório no Claude Code.

Para usá-las em outro projeto baseado neste template, copie a pasta:

```
your-project/
└── .claude/
    └── skills/
        ├── add-feature/
        ├── add-entity/
        ├── add-tests/
        └── ca-review/
```

Funciona tanto na variante padrão quanto na variante Aspire do template.

## Experimente

```
/add-feature adiar uma tarefa até uma data informada
```

O Claude vai criar o command, o validator, o handler (com checagem de dono, evento de domínio e invalidação de cache), o endpoint e os três tipos de teste — e depois compilar e rodar os testes.

## Personalização

Cada skill é um arquivo Markdown simples (`SKILL.md`, mais os modelos em `references/`). Renomeou as camadas, prefere records em tudo, usa outra stack de testes? Edite os modelos uma vez e toda feature futura segue a mudança. As skills são a versão executável do documento de convenções do seu time.
