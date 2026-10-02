# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A pragmatic Clean Architecture starter for .NET 10 (Todos + Users sample domain), targeting PostgreSQL, with JWT auth + refresh tokens, role-based permissions, caching, structured logging, and OpenTelemetry wired in. It is a *template* — the Todos/Users code is sample content meant to be extended or replaced.

Names in this file and in `.claude/skills/` are the template's placeholders (`CleanArchitecture.slnx`, `src/Application`, `TodoItem`). A real solution usually prefixes its projects (e.g. `src/MyApp.Application`, `tests/MyApp.IntegrationTests`); map the placeholders to the actual names in the repository.

## Commands

```bash
# Start infra dependencies only (PostgreSQL + Seq; ports in docker-compose.yml).
# Plain `docker compose up -d` also starts the API container.
docker compose up -d postgres seq

# Run the API (Development applies migrations and runs the seeders on startup)
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

# Add a migration (names are PascalCase_With_Underscores)
dotnet ef migrations add Add_Todos --project src/Infrastructure --startup-project src/Web.Api --output-dir Database/Migrations
```

Seq (structured log viewer) runs with the infra containers; its port is mapped in `docker-compose.yml`.

To target .NET 8 or .NET 9 instead of .NET 10, see the notes in `Directory.Build.props` (also requires updating the Dockerfile in `src/Web.Api`).

Warnings are treated as errors (`TreatWarningsAsErrors`, `AnalysisMode=All`, SonarAnalyzer), so `dotnet build` is a meaningful correctness gate, not just a compile check.

## Architecture

Five projects, dependencies flow strictly inward. This is enforced by `tests/ArchitectureTests/Layers/LayerTests.cs` (via NetArchTest) — Domain and Application must never reference Infrastructure or Web.Api.

```
SharedKernel  <-- Domain <-- Application <-- Infrastructure
                                  ^--------------- Web.Api
```

- **SharedKernel** — DDD primitives with no dependencies on anything else in the solution: `Entity` (base class holding raised domain events), `Result`/`Result<T>`, `Error`/`ErrorType`/`ValidationError`, `IDomainEvent`, `IDomainEventHandler<T>`, `IDateTimeProvider`.
- **Domain** — entities, domain events, and per-aggregate static `*Errors` classes (e.g. `TodoItemErrors`, `UserErrors`), grouped by aggregate folder (`Todos/`, `Users/`), not by technical type. Entities are plain classes with settable properties and foreign-key ids (no navigation properties); business rules live in the Application handlers.
- **Application** — use cases, one per vertical slice folder (e.g. `Todos/Create/`, `Todos/Complete/`), plus `Abstractions/` for cross-cutting interfaces (`Messaging`, `Behaviors`, `Data`, `Authentication`, `Authorization`, `Pagination`) and `Authorization/` for the permission catalog and guards. No MediatR — commands/queries are handled by custom `ICommandHandler`/`IQueryHandler` interfaces resolved via DI + `Scrutor` assembly scanning.
- **Infrastructure** — EF Core (`ApplicationDbContext`, PostgreSQL, snake_case naming, migrations under `Database/Migrations`, seeders under `Database/Seeding`), JWT + refresh tokens, permission provider and authorization policies, `HybridCache`, domain event dispatch.
- **Web.Api** — minimal API endpoints (one class per endpoint implementing `IEndpoint`, auto-registered via assembly scan in `EndpointExtensions`), rate limiting, CORS, OpenTelemetry, Serilog request logging, global exception handling → `ProblemDetails`, Swagger with JWT.

### Vertical slice layout

Each use case lives under `Application/{Aggregate}/{UseCase}/` as a self-contained slice, e.g. `Application/Todos/Create/`:
- `CreateTodoCommand.cs` — the `ICommand<TResponse>` (or `IQuery<TResponse>`) DTO.
- `CreateTodoCommandHandler.cs` — `internal sealed class ... : ICommandHandler<TCommand, TResponse>`. Dependencies via primary constructor (`IApplicationDbContext`, `IUserContext`, `IPermissionProvider`, `IDateTimeProvider`, etc). Returns `Result<T>`, never throws for expected failures.
- `CreateTodoCommandValidator.cs` — `internal sealed` FluentValidation `AbstractValidator<TCommand>`, picked up automatically by `AddValidatorsFromAssembly`. Every command has one; queries don't.
- Queries project straight into a per-slice `{X}Response` DTO with `.Select(...)` and never return entities. Paged lists use `ToPagedResponseAsync` (`Abstractions/Pagination`).

Limits shared by more than one validator of the same aggregate (max lengths, password policy) live in a `{Feature}ValidationRules` static class next to the slices (e.g. `Application/Todos/TodoValidationRules.cs`) instead of being repeated as literals.

The matching endpoint lives in `Web.Api/Endpoints/{Aggregate}/{UseCase}.cs` as an `internal sealed class : IEndpoint` with a nested `Request` DTO, mapping the request to the command/query, calling the handler, and translating `Result` to HTTP via `result.Match(Results.Ok | Results.NoContent, CustomResults.Problem)`.

Tests mirror this: `tests/Application.UnitTests/{Aggregate}/{UseCase}{Command|Query}HandlerTests.cs` for handler unit tests, `{Aggregate}ValidatorsTests.cs` for validators, and `tests/IntegrationTests/{Aggregate}/{Aggregate}Tests.cs` for end-to-end HTTP tests against a Testcontainers PostgreSQL instance.

### Cross-cutting behavior via decorators

`Application/DependencyInjection.cs` wraps handlers with decorators via `Scrutor`'s `.Decorate`. At runtime a request flows **Logging → Validation → handler** (logging is the outermost decorator, so validation failures are logged too). Validation only wraps commands; it short-circuits with a `ValidationError` before the handler runs, so handlers don't re-check input shape — they enforce business rules only.

### Result pattern (no exceptions for expected failures)

`Result` / `Result<T>` in `SharedKernel` is the error-handling convention throughout Domain/Application/Web.Api. Handlers return `Result.Failure<T>(SomeErrors.Reason(...))` instead of throwing. Each aggregate defines its own static errors class (`TodoItemErrors`, `UserErrors`) with codes like `"TodoItems.NotFound"`. `CustomResults.Problem` maps `ErrorType` to status codes: `Validation`/`Problem` → 400, `NotFound` → 404, `Conflict` → 409, `Forbidden` → 403, `Failure` → 500. `GlobalExceptionHandler` handles truly unexpected failures, plus concurrency conflicts and unique-index violations (409) and unreadable requests (400).

### Domain events

Handlers call `entity.Raise(new SomeDomainEvent(...))` before `SaveChangesAsync`. `ApplicationDbContext.SaveChangesAsync` saves first and then `DomainEventsDispatcher` publishes the events, each in its own DI scope and **outside the original transaction**. Handlers implement `IDomainEventHandler<T>` and are auto-registered by assembly scan.

## Authentication and authorization

- **Model:** Users → Roles → Permissions. Permission codes (e.g. `"todos:manage"`) are catalogued in `Application/Authorization/PermissionCodes.cs` with display name, description, group, and an `IsAdministrative` flag. Default roles and their permissions are seeded via `HasData` in the Infrastructure configurations; new users get the default role on registration.
- **Endpoint level:** use `.HasPermission(PermissionCodes.X.Y)`, which fails at startup if the code is not in the catalog. Use plain `.RequireAuthorization()` when the rule depends on the data (ownership).
- **Handler level:** rules that depend on a permission *and* on the data are checked in the handler through `IPermissionProvider` — e.g. "own item needs `todos:update-own`, anyone's needs `todos:manage`", or `IsSelfOrHasPermissionAsync`. Failures return `UserErrors.Unauthorized()` (403).
- **Source of truth:** permissions are always resolved server-side by `IPermissionProvider` (database + `HybridCache` per user). Deactivated users resolve to no permissions. Any command that changes a user's effective permissions (roles, role permissions, activation) must call `permissionProvider.InvalidateAsync(userId)` after saving. Clients read the live list from `GET /users/me`; the API never authorizes from JWT claims.
- **Guards:** `PrivilegeEscalationGuard` blocks granting/removing administrative permissions the caller doesn't have; `AdministratorGuard` keeps at least one active administrator.
- **Tokens:** access tokens expire after `Jwt:ExpirationInMinutes`; refresh tokens after `Jwt:RefreshTokenExpirationInDays`. Refresh tokens are stored only as a SHA-256 hash (also a concurrency token), rotated on every refresh, and revoked by logout or password change.

## Conventions to preserve when extending

- All user-facing text (error descriptions, validation messages) is in Brazilian Portuguese; code, identifiers, error codes, and log templates stay in English.
- New use cases go in `Application/{Aggregate}/{UseCase}/` following the Command/Handler/Validator triad above — don't introduce MediatR.
- New entities get their own `{Entity}Errors` static class in Domain rather than throwing raw exceptions or reusing another aggregate's errors.
- EF Core configurations (`IEntityTypeConfiguration<T>`) live in `Infrastructure/{Aggregate}/{Entity}Configuration.cs` and are picked up by `ApplyConfigurationsFromAssembly`; new `DbSet`s go in `IApplicationDbContext`, `ApplicationDbContext`, and the unit tests' `TestDbContext`.
- Endpoints are one class per route in `Web.Api/Endpoints/{Aggregate}/`, tagged via `Web.Api/Endpoints/Tags.cs`, and registered automatically — no manual route table to update.
- This repo has `.claude/skills/` (`add-entity`, `add-feature`, `add-tests`, `ca-review`) that encode these conventions as executable scaffolding — prefer them over freehand implementations so new code matches the existing slices exactly.
