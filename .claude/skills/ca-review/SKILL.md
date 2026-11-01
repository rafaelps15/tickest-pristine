---
name: ca-review
description: Revisa as mudanças pendentes contra as convenções do projeto — limites entre camadas, domínio rico, tratamento de erro com Result, estrutura dos slices, validação, autorização, endpoints, comentários e cobertura de testes. Use quando o usuário pedir para revisar mudanças, conferir convenções ou auditar uma feature antes do commit.
argument-hint: '[opcional: arquivos ou feature específicos para revisar; o padrão é o diff da árvore de trabalho]'
---

# Revisão de convenções de Clean Architecture

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Revise o escopo informado (padrão: `git diff` + arquivos não rastreados) contra as convenções. Relate os achados com referências `arquivo:linha`, ordenados por gravidade. Não corrija nada a menos que peçam.

## Checklist

### Limites entre camadas (violações são bloqueantes)
- O Domain referencia só o `SharedKernel` — sem EF Core, sem tipos de Application/Infrastructure/Web.Api.
- A Application referencia só Domain + SharedKernel; acesso a dados exclusivamente pelo `IApplicationDbContext`; nenhum `using Infrastructure.*` na Application.
- Os detalhes de persistência (chaves, conversões, relacionamentos, mapeamento por campo) ficam nas classes `IEntityTypeConfiguration<>` da Infrastructure, não nas entidades.
- Os endpoints da Web.Api não têm lógica de negócio — só o mapeamento requisição→command e o match do resultado.

### Domínio rico
- Entidades: um único construtor privado sem parâmetros; o `Create` monta a entidade com inicializador de objeto; `private set` em toda propriedade, coleções num campo privado expostas como `IReadOnlyCollection<T>`.
- `Create` devolve a entidade quando a criação não pode falhar e `Result<T>` quando uma regra pode recusá-la; dispara o evento `Created`.
- As mudanças de estado passam por métodos da entidade que conferem tudo antes da primeira mudança, devolvem `Result` (pode falhar), `bool` (pode não mudar nada, com evento só quando mudou) ou `void` (o handler já conferiu o estado, sem repetir a conferência) e disparam o próprio evento; a hora atual é parâmetro. Uma chamada que falha deixa o estado e os eventos intactos.
- Nenhuma guarda na entidade: o validator confere o formato da entrada; as falhas que o usuário pode provocar e que o validator não enxerga são `Result.Failure(...)`.
- Nenhum handler atribui as propriedades de uma entidade nem chama `Raise` por ela (bloqueante para código novo). O contexto preenche `CreatedOnUtc` / `ModifiedOnUtc` / `DeletedOnUtc`.
- Registros filho, de histórico ou de auditoria são criados pelo método do agregado e adicionados pelo handler antes do único `SaveChangesAsync` — nunca num handler de evento de domínio, nunca montados por uma classe estática.
- Nenhuma classe estática, `*Service`, `*Helper`, `*Manager` ou `*Clock` na Application muda ou inspeciona o estado de uma entidade: o comportamento vira membro da entidade (bloqueante para código novo). Trabalho técnico (hash, arquivos, bytes, ZIP) é uma interface em `Application/Abstractions/` com implementação `internal sealed` na Infrastructure.
- As queries nunca chamam um método da entidade dentro de `Where` ou `Select`; usam um predicado escrito na consulta ou um `Expression<Func<T, bool>>`.
- Objetos de valor herdam `ValueObject` e são criados por `Create(...)` devolvendo `Result<T>`.
- Eventos de domínio são `sealed record` com ids (mais o valor anterior numa alteração), sem `OccurredOnUtc`. Handlers de evento: um por reação, `{Action}On{Event}DomainEventHandler`, numa pasta com o nome do evento.

### Estrutura do slice
- Uma pasta por caso de uso em `src/Application/{Feature}/{UseCase}/`; o endpoint espelha em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`.
- Nomes: `{Verb}{Entity}Command` / `Get{X}Query` / `...Handler` / `...Validator` / `{X}Response`.
- Handlers são `internal sealed` com construtor primário, implementando os `ICommandHandler<>`/`IQueryHandler<>` próprios — sem MediatR, sem registro manual na DI para handlers/validators/endpoints.
- Os DTOs de resposta são por slice; as queries projetam com `.Select(...)` e nunca devolvem entidades de domínio.
- Uma entidade que o `Create` referencia é carregada pelo handler e passada a ele, com `NotFound` quando falta; uma pergunta sim ou não é um `AnyAsync` direto no `if`; nenhuma classe estática faz essas consultas.

### Tratamento de erro
- Falhas esperadas devolvem `Result`/`Result<T>` — sem exceção para controle de fluxo, sem try/catch em volta de regras de negócio.
- Os erros vêm de fábricas estáticas em `{Entity}Errors`, com códigos `"{Feature}.{Reason}"` e o tipo semanticamente correto (`NotFound`/`Conflict`/`Problem`/`Failure`).
- Os endpoints convertem falhas só por `result.Match(Results.Ok|NoContent, CustomResults.Problem)`.

### Validação e autorização
- Todo command tem um `{Command}Validator` do FluentValidation para o formato da entrada; as regras sobre o estado da entidade ficam na entidade. O tamanho das colunas fica só na configuração do EF (`HasMaxLength`), nunca como regra de validator nem como constante na entidade.
- Permissão fixa no endpoint (`.HasPermission(...)`); regra sobre o próprio estado da entidade no método dela; permissões efetivas pelo `IPermissionProvider`; conferência que precisa de consulta num método privado `HasPermissionTo{Action}` do handler. Nenhuma consulta dentro de uma entidade.
- Nenhuma classe estática de guard nova (bloqueante para código novo); uma conferência compartilhada só depois de a repetição ser mostrada, como instância injetada. Uma conferência movida devolve o mesmo erro e o mesmo status de antes.
- Endpoints novos chamam `.RequireAuthorization()` (ou `.HasPermission(...)`) e `.WithTags(Tags.X)`.
- Nenhum `DateTime.UtcNow`/`DateTime.Now` na Application — use `IDateTimeProvider`.

### Cache
- Toda leitura em cache no `HybridCache` tem a invalidação correspondente (`cache.RemoveAsync`) em todo command que altera esses dados; as chaves vêm da classe `{Feature}CacheKeys`.

### Comentários
- Identificadores em inglês; um `<summary>` curto em português só onde explica algo: em toda interface e nos métodos cujo comportamento não é óbvio pelo nome e pela assinatura (o que acontece em falha, quando devolve `false`, efeitos colaterais). Nada em propriedades, construtores, DTOs, endpoints, testes e métodos simples; nunca vazio. Outros comentários só onde o código não se explica, em português. Nenhum comentário cita id de issue ou de spec. Nenhum `<summary>` existente é removido.

### Testes
- Entidades: invariante na construção, cada transição válida, cada transição inválida com estado e eventos inalterados.
- Handlers: todo caminho `Result.Failure` mais o caminho feliz (estado gravado + eventos de domínio). Validators: testes `TestValidate` por regra.
- Endpoints: testes de integração por HTTP real, com a persistência conferida por um GET e todo predicado de consulta coberto por um teste de tradução.
- Os nomes dos testes e a estrutura Arrange/Act/Assert seguem os testes existentes.

### Desvios e regras do projeto
- Confira cada desvio e cada regra permanente declarados no `CLAUDE.md` do repositório. Violação de regra permanente é **Violação de convenção**; se for de segurança ou autorização, é **Bloqueante**. Um desvio declarado que a convenção exige seguir e que não foi seguido também é **Violação de convenção**.
- Quando a revisão for de uma spec, o escopo são os arquivos do plano técnico e das tarefas dela; liste à parte as mudanças pendentes fora desse escopo, sem revisá-las.

### Não pode aparecer
- Repositórios, `IUnitOfWork`, um behavior de transação, MediatR, `Maybe<T>`, controllers, uma classe central `DomainErrors`.

## Formato da saída

Agrupe os achados em **Bloqueantes** (violações de camada, falta de autorização, exceções lançadas para falhas esperadas, handler mudando o estado da entidade), **Violações de convenção** (nomes, estrutura, códigos de erro, eventos/invalidação faltando, comentários) e **Lacunas de teste**. Para cada um: `arquivo:linha`, o que está errado e a correção em uma linha. Termine com um veredito: pronto para commit, ou o que precisa mudar antes. Se tudo passar, diga isso e rode `dotnet build` + `dotnet test` para confirmar.
