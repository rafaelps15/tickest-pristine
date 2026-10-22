---
name: ca-review
description: Revisa as mudanças pendentes contra as convenções do projeto — limites entre camadas, tratamento de erro com Result, estrutura dos slices, validação, endpoints e cobertura de testes. Use quando o usuário pedir para revisar mudanças, conferir convenções ou auditar uma feature antes do commit.
argument-hint: [opcional: arquivos ou feature específicos para revisar; o padrão é o diff da árvore de trabalho]
---

# Revisão das convenções do projeto

> **Contexto do projeto primeiro.** Antes de agir, leia a seção "Contexto do projeto" do `CLAUDE.md` do
> repositório. Os caminhos, namespaces, nomes e exemplos desta skill vêm do modelo de referência (Todos) e
> são ilustrativos. Quando o contexto do projeto declarar caminhos, referências vivas ou desvios, eles
> prevalecem. Se essa seção não existir, inspecione o repositório, siga o slice real mais parecido e
> pergunte antes de assumir uma convenção.

Revise o escopo informado (padrão: `git diff` + arquivos não versionados) contra as convenções do projeto. Relate os achados com referências arquivo:linha, ordenados por gravidade. Não corrija nada, a menos que peçam.

## Checklist

### Limites entre camadas (violações são bloqueantes)
- Domain referencia só o `SharedKernel` — nada de EF Core, nem de tipos de Application/Infrastructure/Web.Api.
- Application referencia só Domain + SharedKernel; acesso a dados exclusivamente pelo `IApplicationDbContext`; nenhum `using Infrastructure.*` em lugar nenhum da Application.
- Detalhes de persistência (chaves, conversões, relacionamentos) ficam nas classes `IEntityTypeConfiguration<>` da Infrastructure, não nas entidades.
- Os endpoints da Web.Api não têm regra de negócio — só o mapeamento requisição→command e a conversão do resultado.

### Estrutura dos slices
- Uma pasta por caso de uso em `src/Application/{Feature}/{UseCase}/`; o endpoint espelha a estrutura em `src/Web.Api/Endpoints/{Feature}/{UseCase}.cs`.
- Nomes: `{Verb}{Entity}Command` / `Get{X}Query` / `...Handler` / `...Validator` / `{X}Response`.
- Handlers são `internal sealed` com construtor primário, implementando os `ICommandHandler<>`/`IQueryHandler<>` próprios — sem MediatR, sem registro manual na DI de handlers/validators/endpoints.
- Os DTOs de resposta são de cada slice; queries projetam com `.Select(...)` e nunca retornam entidades de domínio.

### Tratamento de erro
- Falhas esperadas retornam `Result`/`Result<T>` — sem exceções para controle de fluxo, sem try/catch em volta de regras de negócio.
- Os erros vêm das fábricas estáticas de `{Entity}Errors`, com códigos `"{Feature}.{Reason}"` e o tipo semanticamente correto (`NotFound`/`Conflict`/`Problem`/`Failure`).
- Os endpoints convertem falhas só por `result.Match(Results.Ok|NoContent, CustomResults.Problem)`.

### Validação e segurança
- Todo command tem um `{Command}Validator` do FluentValidation; os handlers não reconferem o formato da entrada (mas aplicam as regras de negócio).
- Handlers que agem sobre dados de um usuário garantem a posse: filtram por `IUserContext.UserId` ou retornam `UserErrors.Unauthorized()`. Se o projeto declarar outras formas de autorização (permissão na rota, guards por recurso), siga o `CLAUDE.md`.
- Endpoints novos chamam `.RequireAuthorization()` (ou `.HasPermission(...)`) e `.WithTags(Tags.X)`.
- Nada de `DateTime.UtcNow`/`DateTime.Now` na Application — use `IDateTimeProvider`.

### Mudanças de estado e cache
- Commands que mudam estado disparam um evento de domínio com `entity.Raise(new XDomainEvent(id))` antes do `SaveChangesAsync`.
- Quando o projeto usar cache em leituras, toda leitura em cache no `HybridCache` tem a invalidação correspondente (`cache.RemoveAsync`) em todo command que altera aqueles dados; as chaves vêm da classe `{Feature}CacheKeys`.

### Testes
- Handlers novos ou alterados têm testes unitários cobrindo cada caminho de `Result.Failure`, mais o caminho feliz (estado salvo + eventos de domínio).
- Validators novos ou alterados têm testes com `TestValidate` por regra.
- Endpoints novos ou alterados têm testes de integração por HTTP de verdade.
- Os nomes dos testes e a estrutura Arrange/Act/Assert seguem os testes existentes.

## Formato da saída

Agrupe os achados em **Bloqueantes** (violações de camada, falta de autorização, exceções lançadas para falhas esperadas), **Violações de convenção** (nomes, estrutura, códigos de erro, eventos/invalidação faltando) e **Lacunas de teste**. Para cada um: `arquivo:linha`, o que está errado e a correção em uma linha. Termine com um veredito: pronto para commit, ou o que precisa mudar antes. Se tudo passar, diga isso e rode `dotnet build` + `dotnet test` para confirmar.
