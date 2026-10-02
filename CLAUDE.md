# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A pragmatic Clean Architecture starter for .NET 10 (Todos + Users sample domain), targeting PostgreSQL, with JWT auth, permission-based authorization, caching, structured logging, and OpenTelemetry wired in. It is a *template* — the Todos/Users code is sample content meant to be extended or replaced.

## Commands

```bash
# Start infra dependencies (PostgreSQL + Seq) before running the API or integration tests
docker compose up -d

# Run the API
dotnet run --project src/Web.Api

# Build / restore
dotnet restore CleanArchitecture.slnx
dotnet build CleanArchitecture.slnx

# Run the full test suite (integration tests spin up a throwaway PostgreSQL container via
# Testcontainers, so Docker must be running)
dotnet test CleanArchitecture.slnx

# Run a single test project
dotnet test tests/Application.UnitTests
dotnet test tests/IntegrationTests
dotnet test tests/ArchitectureTests

# Run a single test by name (any project)
dotnet test --filter "FullyQualifiedName~CreateTodoCommandHandlerTests"
```

Seq (structured log viewer) is available at http://localhost:8081 once `docker compose up -d` is running.

To target .NET 8 or .NET 9 instead of .NET 10, see the notes in `Directory.Build.props` (also requires updating the Dockerfile in `src/Web.Api`).

Warnings are treated as errors (`TreatWarningsAsErrors`, `AnalysisMode=All`), so `dotnet build` is a meaningful correctness gate, not just a compile check.

## Architecture

Five projects, dependencies flow strictly inward. This is enforced by `tests/ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain and Application must never reference Infrastructure or Web.Api.

```
SharedKernel  <-- Domain <-- Application <-- Infrastructure
                                  ^--------------- Web.Api
```

- **SharedKernel** — cross-cutting DDD primitives with no dependencies on anything else in the solution: `Entity` (base class holding raised domain events), `Result`/`Result<T>` (the error-handling convention — see below), `Error`/`ErrorType`, `IDomainEvent`, `IDateTimeProvider`.
- **Domain** — entities, domain events, and per-aggregate static `*Errors` classes (e.g. `TodoItemErrors`, `UserErrors`). No framework dependencies. Grouped by aggregate folder (`Todos/`, `Users/`), not by technical type.
- **Application** — use cases, one per vertical slice folder (e.g. `Todos/Create/`, `Todos/Complete/`), plus `Abstractions/` for cross-cutting interfaces (`Messaging`, `Behaviors`, `Data`, `Authentication`). No MediatR — commands/queries are handled by custom `ICommandHandler`/`IQueryHandler` interfaces resolved via DI + `Scrutor` assembly scanning.
- **Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, snake_case naming convention, migrations), JWT auth + refresh token rotation, permission-based authorization, `HybridCache`, Serilog, domain event dispatch.
- **Web.Api** — minimal API endpoints (one class per endpoint implementing `IEndpoint`, auto-registered via assembly scan in `EndpointExtensions`), rate limiting, OpenTelemetry, global exception handling → `ProblemDetails`, Swagger/OpenAPI with JWT.

### Vertical slice layout

Each use case lives under `Application/{Aggregate}/{UseCase}/` as a self-contained slice, e.g. `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — the `ICommand<TResponse>` (or `IQuery<TResponse>`) DTO.
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Injected dependencies via primary constructor (`IApplicationDbContext`, `IUserContext`, `IDateTimeProvider`, etc). Returns `Result<T>`, never throws for expected failures.
- `CreateTodoCommandValidator.cs` — FluentValidation `AbstractValidator<TCommand>`, picked up automatically by `AddValidatorsFromAssembly`.

The matching endpoint lives in `Web.Api/Endpoints/{Aggregate}/{Verb}.cs` as an `internal sealed class : IEndpoint` with a nested `Request` DTO, mapping the request to the command/query, calling the handler, and translating `Result` to HTTP via `result.Match(Results.Ok, CustomResults.Problem)`.

Tests mirror this: `tests/Application.UnitTests/{Aggregate}/{UseCase}HandlerTests.cs` for handler unit tests, plus validator tests, and `tests/IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` for end-to-end HTTP tests against a Testcontainers PostgreSQL instance.

### Cross-cutting behavior via decorators

`Application/DependencyInjection.cs` wraps every command/query handler with decorators (via `Scrutor`'s `.Decorate`), applied in this order: **Validation → Logging → handler**. Validation short-circuits on failure and returns `Result.Failure` with a `ValidationError` before the handler ever runs — handlers themselves don't need to validate input.

### Result pattern (no exceptions for expected failures)

`Result` / `Result<T>` in `SharedKernel` is the error-handling convention throughout Domain/Application/Web.Api. Handlers return `Result.Failure<T>(SomeErrors.Reason(...))` instead of throwing. Each aggregate defines its own static errors class (`TodoItemErrors`, `UserErrors`) with named factory methods. Endpoints convert the `Result` to an HTTP response via `CustomResults.Problem` (maps `ErrorType` to the appropriate `ProblemDetails` status code). Exceptions are reserved for truly unexpected failures and are caught by `GlobalExceptionHandler`.

### Domain events

Entities inherit from `Entity` and call `Raise(new SomeDomainEvent(...))` inside their own state-changing methods or from handlers at creation time. `DomainEventsDispatcher` (Infrastructure) dispatches raised events after `SaveChangesAsync`; handlers implement `IDomainEventHandler<T>` and are auto-registered by assembly scan.

## Conventions to preserve when extending

- New use cases go in `Application/{Aggregate}/{UseCase}/` following the Command/Handler/Validator triad above — don't introduce MediatR.
- New entities get their own `{Entity}Errors` static class in Domain rather than throwing raw exceptions or reusing another aggregate's errors.
- EF Core configurations (`IEntityTypeConfiguration<T>`) live in `Infrastructure/{Aggregate}/{Entity}Configuration.cs` and must be added to `ApplicationDbContext`.
- Endpoints are one class per route in `Web.Api/Endpoints/{Aggregate}/`, tagged via `Web.Api/Endpoints/Tags.cs`, and registered automatically — no manual route table to update.
- This repo has `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`) that encode these conventions as executable scaffolding — prefer them over freehand implementations so new code matches the existing slices exactly.
