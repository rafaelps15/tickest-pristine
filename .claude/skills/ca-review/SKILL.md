---
name: ca-review
description: Revisa as mudanças pendentes contra as convenções do template de Clean Architecture — limites entre camadas, tratamento de erro com Result, estrutura dos slices, validação, endpoints e cobertura de testes. Use quando o usuário pedir para revisar mudanças, conferir convenções ou auditar uma feature antes do commit.
argument-hint: [opcional: arquivos ou feature específicos; por padrão, o diff do working tree]
---

# Revisão das convenções de Clean Architecture

Revise o escopo informado (por padrão: `git diff` + arquivos não versionados) contra as convenções deste template. Aponte os problemas com referência `arquivo:linha`, em ordem de gravidade. Não corrija nada a menos que isso seja pedido. Escreva o relatório em português.

## Checklist

### Limites entre camadas (violações são bloqueantes)
- O Domain referencia só o `SharedKernel` — nada de EF Core nem de tipos da Application, Infrastructure ou Web.Api.
- A Application referencia só Domain + SharedKernel; acesso a dados exclusivamente via `IApplicationDbContext`; nenhum `using Infrastructure.*` na Application.
- Detalhes de persistência (chaves, conversões, relacionamentos) ficam nas classes `IEntityTypeConfiguration<>` da Infrastructure, não nas entidades.
- Endpoints da Web.Api não têm regra de negócio — só mapeiam a requisição para o command e traduzem o resultado.

### Estrutura dos slices
- Uma pasta por caso de uso em `src/Application/{Feature}/{UseCase}/`; o endpoint espelha isso em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`.
- Nomes: `{Verb}{Entity}Command` / `Get{X}Query` / `...Handler` / `...Validator` / `{X}Response`.
- Handlers são `internal sealed`, com construtor primário, implementando os `ICommandHandler<>`/`IQueryHandler<>` próprios — sem MediatR e sem registro manual na DI de handlers, validators ou endpoints.
- DTOs de resposta são por slice; queries projetam com `.Select(...)` e nunca retornam entidades de domínio.

### Tratamento de erro
- Falhas esperadas retornam `Result`/`Result<T>` — sem exceções para controle de fluxo e sem try/catch em volta de regra de negócio.
- Os erros vêm de métodos estáticos de `{Entity}Errors`, com códigos `"{Feature}.{Reason}"` e o tipo semanticamente correto (`NotFound`/`Conflict`/`Forbidden`/`Problem`/`Failure`).
- Endpoints traduzem falhas só via `result.Match(Results.Ok|NoContent, CustomResults.Problem)`.

### Validação e segurança
- Todo command tem um `{Command}Validator` do FluentValidation, `internal sealed`; os handlers não reconferem o formato da entrada (mas aplicam as regras de negócio). Limites usados por vários validators vêm da classe `{Feature}ValidationRules` da feature, e não de números repetidos.
- Texto mostrado ao usuário (descrições de erro, mensagens de validação) fica em português do Brasil; os códigos de erro continuam em inglês.
- Handlers que lidam com dados de um usuário garantem o acesso: filtram por `IUserContext.UserId` ou retornam `UserErrors.Unauthorized()`. Regras que dependem de uma permissão *e* do dado (o próprio item vs. o de qualquer pessoa) são conferidas no handler via `IPermissionProvider`.
- Endpoints novos chamam `.RequireAuthorization()` (ou `.HasPermission(...)`) e `.WithTags(Tags.X)`.
- Nada de `DateTime.UtcNow`/`DateTime.Now` na Application — use `IDateTimeProvider`.

### Mudanças de estado e cache
- Commands que alteram estado disparam um evento de domínio com `entity.Raise(new XDomainEvent(id))` antes do `SaveChangesAsync`.
- Commands que mudam as permissões efetivas de um usuário (funções, permissões de uma função, ativação) chamam `permissionProvider.InvalidateAsync(userId)` depois de salvar.
- Toda leitura com cache em `HybridCache` tem a invalidação correspondente (`cache.RemoveAsync`) em todos os commands que alteram aquele dado; as chaves vêm da classe `{Feature}CacheKeys`.

### Testes
- Handlers novos ou alterados têm testes unitários cobrindo cada caminho `Result.Failure` e o caminho feliz (estado gravado + eventos de domínio).
- Validators novos ou alterados têm testes com `TestValidate` para cada regra.
- Endpoints novos ou alterados têm testes de integração por HTTP real.
- Nomes dos testes e estrutura Arrange/Act/Assert seguem os testes existentes.

## Formato do relatório

Agrupe os problemas em **Bloqueantes** (violação de camada, falta de autenticação, exceção lançada para falha esperada), **Violações de convenção** (nomes, estrutura, códigos de erro, falta de evento ou de invalidação de cache) e **Lacunas de teste**. Para cada um: `arquivo:linha`, o que está errado e a correção em uma linha. Termine com um veredito: pronto para commit, ou o que precisa mudar antes. Se estiver tudo certo, diga isso e rode `dotnet build` + `dotnet test` para confirmar.
