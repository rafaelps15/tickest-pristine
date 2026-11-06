# Architectural Decision: Migration from Entity Framework Core to Dapper

> Fonte da spec 043, apresentada por Rafael Santos no chat em 2026-10-30, transcrita sem alteração.

## 1. Objective

The application will migrate its persistence implementation from Entity Framework Core (EF Core) to Dapper.

Dapper will become the exclusive data access technology for application persistence.

The objective is to establish a consistent, explicit, predictable, and maintainable persistence architecture.

All existing EF Core persistence implementations must be reviewed and migrated to Dapper.

EF Core must not be used for application data access after the migration.

## 2. Mandatory Persistence Standards

The following conventions are mandatory:

- Dapper is the exclusive data access library for application persistence.
- `IDbConnectionFactory` is responsible for creating database connections.
- Query handlers must implement `IQueryHandler<TQuery, TResult>`.
- SQL statements must be explicitly defined in their respective handlers.
- SQL must use interpolated raw string literals (`$"""`).
- SQL column aliases must use `nameof(Response.Property)`.
- Database connections must use `await using`.
- Query parameters must be passed as parameterized objects, never concatenated into SQL.
- Collection queries must use `QueryAsync<T>()` and `.AsList()`.
- Single-record queries must use `QuerySingleOrDefaultAsync<T>()`.
- Missing records must return the appropriate `Result.Failure<T>()`.
- Commands must use Dapper with explicit transaction management whenever required.
- Avoid unnecessary repositories, generic persistence abstractions, and additional service layers.
- Follow the existing project conventions for namespaces, naming, dependency injection, error handling, and code organization.

These standards apply consistently across all application modules.

## 3. Official Query Handler Templates

The following four examples are the official reference implementations for Dapper query handlers.

They must be included in the specification and used as the foundation for the corresponding Skills.

Use `TodoItem` as the generic domain throughout the examples.

The examples must preserve the implementation structure, coding style, formatting, SQL organization, connection lifecycle, Dapper methods, and result handling of the original reference implementations.

They are mandatory templates, not merely illustrative snippets.

### 3.1 GetTodoItemsQueryHandler — Collection Query

Retrieves a collection of TodoItems.

```csharp
internal sealed class GetTodoItemsQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemsQuery, IReadOnlyCollection<TodoItemResponse>>
{
    public async Task<Result<IReadOnlyCollection<TodoItemResponse>>> Handle(
        GetTodoItemsQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 description AS {nameof(TodoItemResponse.Description)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)}
             FROM todo_items
             """;

        List<TodoItemResponse> todoItems = (await connection.QueryAsync<TodoItemResponse>(sql, request)).AsList();

        return todoItems;
    }
}
```

### 3.2 GetTodoItemQueryHandler — Single Record Query

Retrieves a single TodoItem using the parameters defined in the query.

```csharp
internal sealed class GetTodoItemQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemQuery, TodoItemResponse>
{
    public async Task<Result<TodoItemResponse>> Handle(
        GetTodoItemQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 description AS {nameof(TodoItemResponse.Description)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)}
             FROM todo_items
             WHERE title = @Title
             """;

        TodoItemResponse? todoItem = await connection.QuerySingleOrDefaultAsync<TodoItemResponse>(sql, request);

        if (todoItem is null)
        {
            return Result.Failure<TodoItemResponse>(TodoItemErrors.NotFound());
        }

        return todoItem;
    }
}
```

### 3.3 GetTodoItemByIdQueryHandler — Query by Identifier with Not-Found Handling

Retrieves a TodoItem using its identifier and returns an explicit failure when the record does not exist.

```csharp
internal sealed class GetTodoItemByIdQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemByIdQuery, TodoItemResponse>
{
    public async Task<Result<TodoItemResponse>> Handle(
        GetTodoItemByIdQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemResponse.Id)},
                 title AS {nameof(TodoItemResponse.Title)},
                 description AS {nameof(TodoItemResponse.Description)},
                 is_completed AS {nameof(TodoItemResponse.IsCompleted)}
             FROM todo_items
             WHERE id = @TodoItemId
             """;

        TodoItemResponse? todoItem = await connection.QuerySingleOrDefaultAsync<TodoItemResponse>(sql, request);

        if (todoItem is null)
        {
            return Result.Failure<TodoItemResponse>(TodoItemErrors.NotFound(request.TodoItemId));
        }

        return todoItem;
    }
}
```

### 3.4 Additional Projection Query — When Necessary

If a use case requires retrieving a specific projection of a TodoItem rather than its complete response, use an explicit SQL projection mapped directly to the appropriate response DTO.

For example:

```csharp
internal sealed class GetTodoItemSummaryQueryHandler(IDbConnectionFactory dbConnectionFactory)
    : IQueryHandler<GetTodoItemSummaryQuery, TodoItemSummaryResponse>
{
    public async Task<Result<TodoItemSummaryResponse>> Handle(
        GetTodoItemSummaryQuery request,
        CancellationToken cancellationToken)
    {
        await using DbConnection connection = await dbConnectionFactory.OpenConnectionAsync();

        const string sql =
            $"""
             SELECT
                 id AS {nameof(TodoItemSummaryResponse.Id)},
                 title AS {nameof(TodoItemSummaryResponse.Title)},
                 is_completed AS {nameof(TodoItemSummaryResponse.IsCompleted)}
             FROM todo_items
             WHERE id = @TodoItemId
             """;

        TodoItemSummaryResponse? todoItem = await connection.QuerySingleOrDefaultAsync<TodoItemSummaryResponse>(
            sql,
            request);

        if (todoItem is null)
        {
            return Result.Failure<TodoItemSummaryResponse>(TodoItemErrors.NotFound(request.TodoItemId));
        }

        return todoItem;
    }
}
```

This fourth example is an additional reference for projection scenarios. It must not introduce a new mandatory abstraction or duplicate an existing Skill unnecessarily.

## 4. Command Handler Standards

Command handlers must also use Dapper.

Their implementation must follow the same principles:

- Use `IDbConnectionFactory`.
- Use explicit parameterized SQL.
- Manage database connections using `await using`.
- Use transactions when multiple operations must succeed or fail together.
- Preserve the project's established `ICommandHandler` and `Result` conventions.
- Handle database failures and domain errors consistently.
- Do not introduce EF Core for command execution.

The exact command implementation templates must be established after inspecting the existing project conventions and the reference Skills.

Do not invent interfaces, result types, or transaction abstractions without first checking whether the project already provides them.

## 5. Skills Architecture and Reference Project

Mandatory reference project: C:\dev\clean-architecture

Before creating or modifying any Skill, thoroughly inspect the Skills in the reference project.

The reference project must be used as the primary source for the methodology and organization of Skills.

The goal is not simply to copy the content. Reproduce the way Skills are designed, structured, organized, connected, and maintained.

Analyze:

- Directory and file organization.
- Skill structure and section hierarchy.
- Frontmatter and metadata conventions.
- Instruction-writing methodology.
- How responsibilities and rules are defined.
- How implementation examples are presented.
- How reference files are structured.
- How Skills reference other Skills.
- How shared conventions are reused.
- How supporting documentation is linked.
- How examples guide actual implementation.
- How duplication and conflicting instructions are avoided.

Follow the same organizational model, writing style, level of detail, reference strategy, and documentation methodology.

If the reference project uses supporting files, separate reference documents, or cross-Skill links, adopt the same approach whenever applicable.

Do not create an independent Skill architecture when the reference project already establishes one.

### 5.1 Skill Creation Strategy

Before creating a new Skill:

1. Inspect all Skills in the current project.
2. Inspect the corresponding Skills and references in `C:\dev\clean-architecture`.
3. Identify whether an existing Skill can be reused or extended.
4. Identify missing responsibilities that justify new Skills.
5. Avoid redundant Skills and overlapping responsibilities.
6. Establish explicit and consistent references between related Skills.
7. Use generic examples such as `TodoItem`.
8. Include the official QueryHandler templates defined in this specification.
9. Create supporting reference files when necessary, following the reference project's methodology.

The Skills must cover the following responsibilities as necessary:

- Dapper query handlers.
- Dapper command handlers.
- Database connection management.
- SQL conventions and DTO projections.
- Transaction management.
- Database migrations and schema evolution.

The final number and organization of Skills must be determined after inspecting both projects.

Do not create one Skill for every class or implementation type without architectural justification.

### 5.2 Skills Must Be Actionable

Skills must contain practical instructions that an AI coding agent can follow during implementation.

They must explain:

- When to apply each pattern.
- Which existing Skill or reference to consult.
- Which implementation template to follow.
- How to handle common variations.
- Which architectural decisions are mandatory.
- Which practices are prohibited.
- How to validate the implementation.

Do not limit Skills to conceptual descriptions.

Use complete code examples and explicit implementation rules.

When a pattern is already documented in another Skill, reference and reuse it instead of duplicating its entire content.

The examples in this specification are the authoritative baseline for query handlers. Skills must preserve their conventions.

## 6. EF Core Removal

Review and migrate all existing EF Core application persistence implementations.

Evaluate:

- DbContext implementations.
- Entity configurations.
- EF Core repositories.
- EF Core-specific services.
- Dependency registrations.
- Package references.
- Query and command implementations.
- Database initialization.
- Existing migration mechanisms.

Database schema evolution must remain supported.

If EF Core currently provides migration tooling, evaluate whether to retain it exclusively for schema migrations or replace it with an alternative.

Any retained EF Core dependency must not be used for application data access.

The final application persistence architecture must use Dapper exclusively.

## 7. Migration Requirements

The implementation must:

1. Inventory all existing EF Core usages.
2. Map existing persistence responsibilities.
3. Identify the corresponding Dapper implementation for each use case.
4. Migrate existing query handlers.
5. Migrate existing command handlers.
6. Replace EF Core-dependent persistence mechanisms.
7. Create or update the necessary Skills.
8. Follow the Skill organization and methodology of `C:\dev\clean-architecture`.
9. Include the official `TodoItem` examples in the specification and corresponding Skills.
10. Update dependency injection and project references.
11. Update automated tests.
12. Validate SQL mappings, transactions, error handling, and database behavior.
13. Remove obsolete EF Core application persistence code.
14. Update affected specifications and documentation.

Do not perform a superficial migration that merely replaces EF Core calls while preserving unnecessary abstractions.

Do not change unrelated application behavior or introduce unnecessary architectural changes.

## 8. Acceptance Criteria

The migration is complete only when:

- All application persistence uses Dapper.
- No application query or command relies on EF Core.
- Query handlers follow the official implementation templates.
- Command handlers follow the established Dapper conventions.
- Database connection and transaction lifecycles are correctly managed.
- SQL is parameterized.
- Response mappings are explicitly defined.
- Existing application behavior is preserved.
- Automated tests and builds pass.
- Required Skills are created or updated.
- Skills follow the methodology and organizational model of `C:\dev\clean-architecture`.
- Generic examples such as `TodoItem` are used.
- The three mandatory QueryHandler examples are documented and available as implementation references.
- The additional projection example is included where applicable.
- Supporting reference files and cross-Skill links follow the reference project's methodology.
- No obsolete EF Core application persistence implementation remains.
- Specification, Skills, and implementation remain consistent.

Any architectural exception must be explicitly justified and documented.

## 9. Execution and Authorization Constraints

Execute the migration autonomously, acting as a senior .NET architect and developer.

Do not stop to ask questions or request approval for routine architectural and implementation decisions covered by this specification.

Inspect the existing project and reference project before making changes.

Complete the entire scope, including Skills, documentation, implementation, tests, and final validation.

At the end:

- Review all modified files.
- Verify consistency between specification, Skills, and code.
- Run the appropriate builds and tests.
- Report completed work and any unresolved issues.
- Present the final Git working tree status.

Do not stage files, create commits, or push changes.

Leave all Git operations for explicit user authorization.
