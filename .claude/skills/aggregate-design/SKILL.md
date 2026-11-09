---
name: aggregate-design
description: Decide o desenho de um agregado antes de escrevê-lo — a fronteira e o tamanho, quais invariantes valem na transação e quais aceitam consistência eventual, a referência a outros agregados por id, as regras que precisam de dados de fora ou de vários agregados, as coleções internas e a concorrência — e escreve a regra com nome de negócio próprio como classe de regra que devolve Result. Use quando o usuário pedir para desenhar ou dividir um agregado, decidir onde fica uma regra que envolve várias entidades, acrescentar uma coleção interna ou proteger uma invariante contra mudanças simultâneas.
argument-hint: '<agregado ou invariante a desenhar, ex.: "project maintainers">'
---

# Desenho de agregados

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

> **Portão de especificação.** Se o `CLAUDE.md` do repositório adotar um fluxo de especificação antes do
> código (por exemplo, Spec-Driven Development), esta skill só escreve código de produção ou testes dentro
> desse fluxo: chamada pela skill de implementação para uma tarefa de spec aprovada, ou numa mudança
> trivial autorizada explicitamente pelo responsável no chat. Num pedido direto fora desse fluxo, não
> escreva código: procure em `specs/` uma spec que cubra o pedido e indique o comando de implementação
> dela; se não houver, use a skill de especificação.

Um agregado é a fronteira de uma regra de negócio: tudo o que a regra precisa ver de uma vez para dizer sim ou não fica dentro dele, é carregado junto e é gravado num único `SaveChangesAsync`. Esta skill decide a fronteira e onde cada regra mora; a `domain-modeling` e a `add-entity` escrevem o código.

## Workflow

1. **Dê nome à invariante** em palavras de negócio ("um projeto mantém pelo menos um mantenedor"), com toda operação que pode quebrá-la.
2. **Liste os dados que ela lê.** Quando todos ficam numa entidade e nos filhos dela, essa entidade é a raiz, e a regra é um método dela ([references/boundaries.md](references/boundaries.md)).
3. **Traga pelo handler o dado de fora.** As permissões de quem chama ou um valor lido de outro agregado são lidos pelo handler e passados ao método; um cálculo que o domínio não faz é uma interface do Domain passada como argumento (`domain-modeling`, padrão 5); uma regra sobre vários agregados carregados é um serviço de domínio (padrão 6).
4. **Decida a consistência.** A regra que precisa valer no instante da mudança fica num agregado só, ou ganha um ponto de serialização; a que pode ser reparada depois reage a um evento de domínio (`add-feature`, handler de evento de domínio).
5. **Mantenha o agregado pequeno.** O filho que só existe com a raiz e que as invariantes leem fica dentro, como coleção interna; o que tem vida própria é outro agregado, referenciado por id.
6. **Escreva a regra** — um `if` no método, ou uma classe de regra quando ela merece um nome ([references/rules.md](references/rules.md)).
7. **Proteja-a na concorrência** — o token de concorrência da raiz, ou uma linha que as operações concorrentes alteram ([references/concurrency.md](references/concurrency.md)).
8. **Teste pelo método do agregado** (`add-tests`) e revise com a `ca-review`.

## Regras

- **Classe de regra só quando ela merece um nome** — a regra com nome de negócio próprio que recebe dados de fora (permissões, contagens) ou se repete entre métodos vira `{Name}Rule : IBusinessRule` em `Domain/{Feature}/Rules/`; a conferência simples do próprio estado da entidade continua um `if` que devolve o erro do `{Entity}Errors`.
- **Uma classe de regra por invariante** — `internal sealed`, com os dados que ela lê no construtor, `IsBroken()` e o `Error`; ela nunca consulta nada e nunca muda estado.
- **O método confere as regras primeiro e devolve `Result`** — `CheckRule(new ...Rule(...))` antes da primeira mudança; regra quebrada nunca lança exceção.
- **Outros agregados por id** — uma propriedade com o id, nunca uma navegação. Um command muda um agregado, a não ser que um serviço de domínio confira uma regra sobre vários agregados carregados ou que a regra precise de um ponto de serialização.
- **Filhos internos** ficam num campo privado exposto como coleção somente leitura, são criados e removidos só pela raiz e carregados com ela no command (`Include`). O registro filho que as invariantes não leem (histórico, auditoria, uma lista sem limite) fica fora: a raiz o cria e o handler o insere (`domain-modeling`, padrão 7).
- **Token de concorrência na raiz** — todo método que muda o agregado também muda a raiz, então um save concorrente falha; o conflito vira `409`, e o cliente repete a requisição.
- **Os testes passam pelo agregado** — quebram a regra e conferem o `Error`, o estado e os eventos intactos; a classe de regra não tem teste próprio.

## Referência de nomes

| Tipo de regra | Nome | Exemplo |
|---|---|---|
| Quem pode agir | `Only{Who}Can{Action}Rule` | `OnlyProjectManagerCanRemoveMaintainersRule` |
| Mínimo que precisa restar | `{Thing}MustHaveAtLeastOne{Item}Rule` | `ProjectMustHaveAtLeastOneMaintainerRule` |
| Mudança proibida | `{Thing}Cannot{Action}Rule` | `UserCannotOwnMoreThanFiveProjectsRule` |

## Não usado

Regras que lançam exceção (o `CheckRule` devolve `Result`), repositórios e `IUnitOfWork`, navegação para a raiz de outro agregado, entidade que carrega outro agregado e trava pessimista como padrão: ela exige transação explícita, fora destas convenções, e só se usa quando um token de concorrência não expressa o conflito.
