---
name: domain-modeling
description: Leva as regras de negócio para as entidades e os objetos de valor — setters privados, fábricas Create, métodos de comportamento que devolvem Result e disparam os próprios eventos, sem guardas (o validator confere a entrada), objetos de valor, interfaces de domínio passadas como argumento e serviços de domínio para regras entre agregados. Use quando o usuário pedir para criar ou enriquecer uma entidade, tirar uma regra de um handler ou de uma classe estática, ou adicionar um objeto de valor.
argument-hint: '<entidade ou regra a modelar, ex.: "todo item completion" ou "project members">'
---

# Modelagem de domínio

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

> **Portão de especificação.** Se o `CLAUDE.md` do repositório adotar um fluxo de especificação antes do
> código (por exemplo, Spec-Driven Development), esta skill só escreve código de produção ou testes dentro
> desse fluxo: chamada pela skill de implementação para uma tarefa de spec aprovada, ou numa mudança
> trivial autorizada explicitamente pelo responsável no chat (quando o `CLAUDE.md` declarar um marcador de
> autorização, como `[sdd:autorizo]`, ele precisa estar na mensagem do responsável). Num pedido direto fora desse fluxo, não
> escreva código: procure em `specs/` uma spec que cubra o pedido e indique o comando de implementação
> dela; se não houver, use a skill de especificação.

As entidades guardam as próprias regras: o handler carrega a entidade, chama um método dela e salva. O estado nunca muda de fora — nenhum handler e nenhuma classe estática atribui as propriedades de uma entidade ou dispara os eventos dela. As demais convenções do código continuam como estão (`IApplicationDbContext`, `Entity.Raise`, `{Entity}Errors`, eventos com ids).

## Workflow

1. **Ache onde a regra mora hoje.** Procure na Application atribuições às propriedades da entidade e `Raise(new ...)` sobre ela — em handlers e em classes estáticas.
2. **Escreva o método da entidade** com o padrão abaixo (a `add-entity` tem o modelo completo de entidade).
3. **Escreva os testes unitários dele** (`add-tests`).
4. **Troque cada handler** para chamar o método, um por vez, rodando os testes depois de cada um.
5. **Feche os setters** (`private set`) e apague o código que mudou de lugar.
6. **Revise** com a `ca-review`.

## Padrões

Código de cada um: [references/patterns.md](references/patterns.md).

1. **Método de comportamento devolve `Result` e dispara o próprio evento** — `todoItem.Complete(utcNow)`. Toda conferência roda antes da primeira mudança: em falha, o estado e os eventos ficam como estavam. A hora atual é parâmetro.
2. **Fábrica `Create` dispara o evento `Created`** — `Project.Create(...)` devolve a entidade quando a criação não pode falhar; `TodoItem.Create(...)` devolve `Result<TodoItem>` quando uma regra pode recusá-la. Um único construtor privado sem parâmetros; o `Create` monta a entidade com inicializador de objeto.
3. **A entidade não repete o validator** — o validator confere o formato da entrada; o `Create` e os métodos não acrescentam guardas. Uma falha que o usuário pode provocar e que o validator não enxerga é `Result.Failure(...)`.
4. **Objeto de valor com fábrica `Create`** — `ProjectName : ValueObject`, montado com `Result.Create(...).Ensure(...).Map(...)`. O handler cria os objetos de valor e devolve a primeira falha (`Result.FirstFailureOrSuccess(...)`) antes de montar a entidade.
5. **Algo que a entidade não calcula sozinha** — uma interface declarada no Domain, passada ao método que precisa dela (`user.VerifyPassword(password, passwordHashChecker)`); a implementação fica na Infrastructure. Uma classe pode implementá-la junto com a interface da Application para o mesmo trabalho (`PasswordHasher : IPasswordHasher, IPasswordHashChecker`).
6. **Serviço de domínio para regra entre agregados** — uma classe `sealed` de instância no Domain, criada pelo handler (`new TodoItemTransferService()`). Nem estática, nem na Application.
7. **O agregado cria o registro filho; o handler o insere no mesmo save** — `project.AddMember(userId)` monta o `ProjectMember`, dispara `ProjectMemberAddedDomainEvent` e o devolve; o handler o adiciona antes do único `SaveChangesAsync`. Vale também para histórico e auditoria — nunca um handler de evento de domínio, que roda depois de salvar, em outra transação. O filho é montado pelo próprio `Create`, com um inicializador de objeto; ele é `internal` quando só o agregado pode criá-lo.
8. **`Result` para o que pode falhar, `bool` para o que pode não mudar nada** — `Complete` devolve `Result`; `ChangeDescription` devolve `bool` e só dispara `TodoItemDescriptionChangedDomainEvent` quando mudou, levando o valor anterior. Nunca os dois. Quando o handler já conferiu o estado antes da chamada, o método é `void` (`project.Archive()`) e não repete a conferência. Conferências que leem o estado antes da chamada, ou que precisam de consulta ("já é membro"), ficam no handler.
9. **Regras que só leem o estado da entidade são membros dela** — uma estática `TodoItemRules.IsOverdue(todoItem, utcNow)` vira `todoItem.IsOverdue(utcNow)` para uma entidade carregada; uma consulta usa um predicado traduzível no lugar (um `Expression<Func<TodoItem, bool>>` como `TodoItem.OverdueAt(utcNow)`, ou a condição escrita na consulta).
10. **Eventos são records com ids** — `sealed record {Entity}{PastTenseVerb}DomainEvent(Guid {Entity}Id)`; um evento de alteração leva também o valor anterior. Sem `OccurredOnUtc`.

Um `<summary>` curto em português só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio.

## Aplicando a uma classe estática

Uma classe estática da Application que recebe uma entidade e a altera (ex.: `TodoItemManager.Complete(todoItem, utcNow)`) é desmontada membro a membro:

| O membro faz | Padrão |
|---|---|
| Muda o estado da entidade, dispara o evento dela | 1 — método na entidade |
| Cria o agregado | 2 — fábrica `Create` |
| Cria um filho ou um registro de histórico | 7 — criado pelo método do agregado, inserido pelo handler no mesmo save |
| Precisa de algo calculado fora (conferir um hash) | 5 — interface no Domain passada como argumento |
| Abrange vários agregados | 6 — serviço de domínio |
| Responde uma pergunta sobre a entidade (`IsOverdue`, transição permitida) | 9 — propriedade ou método na entidade |
| Monta a resposta da API | O query handler daquele slice, projetando para o `{X}Response` dele |
| Trabalho técnico sem entidade (bytes, arquivos, ZIP, criptografia) | Não é domínio: interface em `Application/Abstractions/{Area}/`, implementação na Infrastructure (`add-feature`, [../add-feature/references/abstraction.md](../add-feature/references/abstraction.md)) |

## Não usado

Repositórios e `IUnitOfWork` (use `IApplicationDbContext`), `Maybe<T>` (use tipos anuláveis), uma classe central `DomainErrors` (use `{Entity}Errors`), eventos com entidades (eles levam ids) e serviços de domínio que lançam exceção para falha de negócio (eles devolvem `Result`).
