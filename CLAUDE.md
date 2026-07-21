# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A ticketing/helpdesk backend for .NET 10 built on a pragmatic Clean Architecture foundation (Tickets, Users, Departments, Sectors), targeting PostgreSQL, with JWT auth, direct per-user permission-based authorization, caching, structured logging, and OpenTelemetry wired in.

## Commands

```bash
# Start infra dependencies (PostgreSQL + Seq) before running the API or integration tests
docker compose up -d

# Run the API
dotnet run --project src/TickestPristine.Web.Api

# Build / restore
dotnet restore TickestPristine.slnx
dotnet build TickestPristine.slnx

# Run the full test suite (integration tests spin up a throwaway PostgreSQL container via
# Testcontainers, so Docker must be running)
dotnet test TickestPristine.slnx

# Run a single test project
dotnet test tests/TickestPristine.Application.UnitTests
dotnet test tests/TickestPristine.IntegrationTests
dotnet test tests/TickestPristine.ArchitectureTests

# Run a single test by name (any project)
dotnet test --filter "FullyQualifiedName~CreateTicketCommandHandlerTests"
```

Seq (structured log viewer) is available at http://localhost:8081 once `docker compose up -d` is running.

To target .NET 8 or .NET 9 instead of .NET 10, see the notes in `Directory.Build.props` (also requires updating the Dockerfile in `src/TickestPristine.Web.Api`).

Warnings are treated as errors (`TreatWarningsAsErrors`, `AnalysisMode=All`), so `dotnet build` is a meaningful correctness gate, not just a compile check.

## Architecture

Five projects, dependencies flow strictly inward. This is enforced by `tests/TickestPristine.ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain and Application must never reference Infrastructure or Web.Api.

```
TickestPristine.SharedKernel  <-- TickestPristine.Domain <-- TickestPristine.Application <-- TickestPristine.Infrastructure
                                                                    ^--------------------------------- TickestPristine.Web.Api
```

- **TickestPristine.SharedKernel** — cross-cutting DDD primitives with no dependencies on anything else in the solution: `Entity` (base class holding raised domain events), `Result`/`Result<T>` (the error-handling convention — see below), `Error`/`ErrorType`, `IDomainEvent`, `IDateTimeProvider`.
- **TickestPristine.Domain** — entities, domain events, and per-aggregate static `*Errors` classes (e.g. `TicketErrors`, `UserErrors`). No framework dependencies. Grouped by aggregate folder (`Tickets/`, `Users/`, `Departments/`, `Sectors/`), not by technical type.
- **TickestPristine.Application** — use cases, one per vertical slice folder (e.g. `Tickets/Create/`, `Tickets/Update/`), plus `Abstractions/` for cross-cutting interfaces (`Messaging`, `Behaviors`, `Data`, `Authentication`, `Authorization`). No MediatR — commands/queries are handled by custom `ICommandHandler`/`IQueryHandler` interfaces resolved via DI + `Scrutor` assembly scanning.
- **TickestPristine.Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, snake_case naming convention, migrations), JWT auth + refresh token rotation, permission-based authorization, `HybridCache`, Serilog, domain event dispatch.
- **TickestPristine.Web.Api** — minimal API endpoints (one class per endpoint implementing `IEndpoint`, auto-registered via assembly scan in `EndpointExtensions`), rate limiting, OpenTelemetry, global exception handling → `ProblemDetails`, Swagger/OpenAPI with JWT.

### Vertical slice layout

Each use case lives under `TickestPristine.Application/{Aggregate}/{UseCase}/` as a self-contained slice, e.g. `TickestPristine.Application/Tickets/Create/`:
- `CreateTicketCommand.cs` — the `ICommand<TResponse>` (or `IQuery<TResponse>`) DTO.
- `CreateTicketCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Injected dependencies via primary constructor (`IApplicationDbContext`, `IUserContext`, `IDateTimeProvider`, `IPermissionProvider`, etc). Returns `Result<T>`, never throws for expected failures.
- `CreateTicketCommandValidator.cs` — FluentValidation `AbstractValidator<TCommand>`, picked up automatically by `AddValidatorsFromAssembly`.

The matching endpoint lives in `TickestPristine.Web.Api/Endpoints/{Aggregate}/{Verb}.cs` as an `internal sealed class : IEndpoint` with a nested `Request` DTO, mapping the request to the command/query, calling the handler, and translating `Result` to HTTP via `result.Match(Results.Ok, CustomResults.Problem)`.

Tests mirror this: `tests/TickestPristine.Application.UnitTests/{Aggregate}/{UseCase}HandlerTests.cs` for handler unit tests, plus validator tests, and `tests/TickestPristine.IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` for end-to-end HTTP tests against a Testcontainers PostgreSQL instance.

### Cross-cutting behavior via decorators

`TickestPristine.Application/DependencyInjection.cs` wraps every command/query handler with decorators (via `Scrutor`'s `.Decorate`), applied in this order: **Validation → Logging → handler**. Validation short-circuits on failure and returns `Result.Failure` with a `ValidationError` before the handler ever runs — handlers themselves don't need to validate input.

### Result pattern (no exceptions for expected failures)

`Result` / `Result<T>` in `TickestPristine.SharedKernel` is the error-handling convention throughout Domain/Application/Web.Api. Handlers return `Result.Failure<T>(SomeErrors.Reason(...))` instead of throwing. Each aggregate defines its own static errors class (`TicketErrors`, `UserErrors`) with named factory methods. Endpoints convert the `Result` to an HTTP response via `CustomResults.Problem` (maps `ErrorType` to the appropriate `ProblemDetails` status code). Exceptions are reserved for truly unexpected failures and are caught by `GlobalExceptionHandler`.

### Domain events

Entities inherit from `Entity` and call `Raise(new SomeDomainEvent(...))` inside their own state-changing methods or from handlers at creation time. `DomainEventsDispatcher` (Infrastructure) dispatches raised events after `SaveChangesAsync`; handlers implement `IDomainEventHandler<T>` and are auto-registered by assembly scan.

## Conventions to preserve when extending

- New use cases go in `TickestPristine.Application/{Aggregate}/{UseCase}/` following the Command/Handler/Validator triad above — don't introduce MediatR.
- New entities get their own `{Entity}Errors` static class in Domain rather than throwing raw exceptions or reusing another aggregate's errors.
- EF Core configurations (`IEntityTypeConfiguration<T>`) live in `TickestPristine.Infrastructure/{Aggregate}/{Entity}Configuration.cs` and must be added to `ApplicationDbContext`.
- Endpoints are one class per route in `TickestPristine.Web.Api/Endpoints/{Aggregate}/`, tagged via `TickestPristine.Web.Api/Endpoints/Tags.cs`, and registered automatically — no manual route table to update.
- This repo has `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`) that encode these conventions as executable scaffolding — prefer them over freehand implementations so new code matches the existing slices exactly.
