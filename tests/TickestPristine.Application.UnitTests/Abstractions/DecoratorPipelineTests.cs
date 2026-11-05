using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TickestPristine.Application.Abstractions.Behaviors;
using TickestPristine.Application.Abstractions.Messaging;
using TickestPristine.Application.Abstractions.Telemetry;

namespace TickestPristine.Application.UnitTests.Abstractions;

// O AddApplication muda opções globais do FluentValidation; a coleção roda sozinha e o teste as devolve no fim.
[CollectionDefinition(nameof(ValidatorGlobalsCollection), DisableParallelization = true)]
public sealed class ValidatorGlobalsCollection;

[Collection(nameof(ValidatorGlobalsCollection))]
public sealed class DecoratorPipelineTests : IDisposable
{
    private readonly CultureInfo? _originalCulture = ValidatorOptions.Global.LanguageManager.Culture;
    private readonly Func<Type, MemberInfo, LambdaExpression, string?> _originalDisplayNameResolver =
        ValidatorOptions.Global.DisplayNameResolver;

    private const string OutcomeError = "Teste.Saida";
    private const string ResultTag = "tickest.request.result";
    private const string RequestFailures = "tickest.request.failures";

    private readonly PipelineCalls _calls = new();
    private readonly TestMeterFactory _meterFactory = new();

    [Fact]
    public void AddApplication_Should_ResolveValidationThenLogging_When_CommandHandlerIsRequested()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        Type[] chain = Chain(scope.ServiceProvider.GetRequiredService<ICommandHandler<PipelineCommand>>());

        // Assert
        chain.ShouldBe(
        [
            typeof(ValidationDecorator.CommandBaseHandler<PipelineCommand>),
            typeof(LoggingDecorator.CommandBaseHandler<PipelineCommand>),
            typeof(PipelineCommandHandler)
        ]);
    }

    [Fact]
    public void AddApplication_Should_ResolveValidationThenLogging_When_CommandWithResponseHandlerIsRequested()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        Type[] chain = Chain(scope.ServiceProvider.GetRequiredService<ICommandHandler<PipelineCommandWithResponse, Guid>>());

        // Assert
        chain.ShouldBe(
        [
            typeof(ValidationDecorator.CommandHandler<PipelineCommandWithResponse, Guid>),
            typeof(LoggingDecorator.CommandHandler<PipelineCommandWithResponse, Guid>),
            typeof(PipelineCommandWithResponseHandler)
        ]);
    }

    [Fact]
    public void AddApplication_Should_ResolveOnlyLogging_When_QueryHandlerIsRequested()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        // Act
        Type[] chain = Chain(scope.ServiceProvider.GetRequiredService<IQueryHandler<PipelineQuery, int>>());

        // Assert
        chain.ShouldBe([typeof(LoggingDecorator.QueryHandler<PipelineQuery, int>), typeof(PipelineQueryHandler)]);
    }

    [Fact]
    public async Task Handle_Should_NotCallHandler_When_CommandIsInvalid()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<PipelineCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PipelineCommand>>();

        // Act
        Result result = await handler.Handle(new PipelineCommand(string.Empty), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBeOfType<ValidationError>();
        _calls.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_Should_CallHandlerOnce_When_CommandIsValid()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<PipelineCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PipelineCommand>>();

        // Act
        Result result = await handler.Handle(new PipelineCommand("válido"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _calls.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(null, LogLevel.Information)]
    [InlineData(ErrorType.NotFound, LogLevel.Information)]
    [InlineData(ErrorType.Conflict, LogLevel.Information)]
    [InlineData(ErrorType.Problem, LogLevel.Information)]
    [InlineData(ErrorType.Forbidden, LogLevel.Warning)]
    [InlineData(ErrorType.Failure, LogLevel.Error)]
    public async Task Handle_Should_LogCompletionWithLevelOfOutcome_When_CommandEnds(ErrorType? failure, LogLevel expectedLevel)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<OutcomeCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<OutcomeCommand>>();

        // Act
        await handler.Handle(new OutcomeCommand(failure), CancellationToken.None);

        // Assert
        TestLogEntry completion = Logger<LoggingDecorator.CommandBaseHandler<OutcomeCommand>>(provider).Entries[^1];
        completion.Level.ShouldBe(expectedLevel);
        completion.Message.ShouldContain(nameof(OutcomeCommand));
        completion.Message.ShouldContain(" ms");
        completion.Message.Contains(OutcomeError, StringComparison.Ordinal).ShouldBe(failure is not null);
    }

    [Theory]
    [InlineData(null, LogLevel.Information)]
    [InlineData(ErrorType.NotFound, LogLevel.Information)]
    [InlineData(ErrorType.Forbidden, LogLevel.Warning)]
    [InlineData(ErrorType.Failure, LogLevel.Error)]
    public async Task Handle_Should_LogCompletionWithLevelOfOutcome_When_QueryEnds(ErrorType? failure, LogLevel expectedLevel)
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryHandler<OutcomeQuery, int> handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<OutcomeQuery, int>>();

        // Act
        await handler.Handle(new OutcomeQuery(failure), CancellationToken.None);

        // Assert
        TestLogEntry completion = Logger<LoggingDecorator.QueryHandler<OutcomeQuery, int>>(provider).Entries[^1];
        completion.Level.ShouldBe(expectedLevel);
        completion.Message.ShouldContain(nameof(OutcomeQuery));
        completion.Message.ShouldContain(" ms");
    }

    [Fact]
    public async Task Handle_Should_NotLog_When_CommandIsInvalid()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<PipelineCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PipelineCommand>>();

        // Act
        await handler.Handle(new PipelineCommand(string.Empty), CancellationToken.None);

        // Assert
        Logger<LoggingDecorator.CommandBaseHandler<PipelineCommand>>(provider).Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_Should_EmitSpansAndCountFailureByType_When_OneCommandSucceedsAndAnotherFailsWithConflict()
    {
        // Arrange
        using var spans = new ActivityRecorder();
        using var measurements = new MeasurementRecorder(_meterFactory);
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<OutcomeCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<OutcomeCommand>>();

        // Act
        await handler.Handle(new OutcomeCommand(null), CancellationToken.None);
        await handler.Handle(new OutcomeCommand(ErrorType.Conflict), CancellationToken.None);

        // Assert
        Activity[] commandSpans = spans.Named(nameof(OutcomeCommand));
        commandSpans.Length.ShouldBe(2);
        commandSpans.ShouldAllBe(span => span.Source.Name == ApplicationTelemetry.Name);
        commandSpans[0].GetTagItem(ResultTag).ShouldBe("success");
        commandSpans[1].GetTagItem(ResultTag).ShouldBe(OutcomeError);
        commandSpans[1].GetTagItem("error.type").ShouldBe(nameof(ErrorType.Conflict));
        commandSpans[1].Status.ShouldBe(ActivityStatusCode.Unset);
        measurements.Sum(RequestFailures, "error.type", nameof(ErrorType.Conflict)).ShouldBe(1);
        measurements.Sum(RequestFailures, "tickest.request.name", nameof(OutcomeCommand)).ShouldBe(1);
        measurements.Sum(RequestFailures).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_EmitSpanWithResult_When_QueryRuns()
    {
        // Arrange
        using var spans = new ActivityRecorder();
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IQueryHandler<OutcomeQuery, int> handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<OutcomeQuery, int>>();

        // Act
        await handler.Handle(new OutcomeQuery(null), CancellationToken.None);

        // Assert
        Activity span = spans.Named(nameof(OutcomeQuery)).ShouldHaveSingleItem();
        span.Source.Name.ShouldBe(ApplicationTelemetry.Name);
        span.GetTagItem(ResultTag).ShouldBe("success");
    }

    [Fact]
    public async Task Handle_Should_MarkSpanAsError_When_CommandFailsUnexpectedly()
    {
        // Arrange
        using var spans = new ActivityRecorder();
        using var measurements = new MeasurementRecorder(_meterFactory);
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<OutcomeCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<OutcomeCommand>>();

        // Act
        await handler.Handle(new OutcomeCommand(ErrorType.Failure), CancellationToken.None);

        // Assert
        Activity span = spans.Named(nameof(OutcomeCommand)).ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(ResultTag).ShouldBe(OutcomeError);
        measurements.Sum(RequestFailures, "error.type", nameof(ErrorType.Failure)).ShouldBe(1);
    }

    [Fact]
    public async Task Handle_Should_MarkSpanAsErrorAndRethrow_When_HandlerThrows()
    {
        // Arrange
        using var spans = new ActivityRecorder();
        using var measurements = new MeasurementRecorder(_meterFactory);
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        ICommandHandler<ThrowingCommand> handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ThrowingCommand>>();

        // Act
        Exception? exception = await Record.ExceptionAsync(() => handler.Handle(new ThrowingCommand(), CancellationToken.None));

        // Assert
        exception.ShouldBeOfType<InvalidOperationException>();
        Activity span = spans.Named(nameof(ThrowingCommand)).ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.Events.ShouldContain(spanEvent => spanEvent.Name == "exception");
        measurements.Sum(RequestFailures).ShouldBe(0);
    }

    public void Dispose()
    {
        ValidatorOptions.Global.LanguageManager.Culture = _originalCulture;
        ValidatorOptions.Global.DisplayNameResolver = _originalDisplayNameResolver;
        _meterFactory.Dispose();
    }

    private static TestLogger<T> Logger<T>(ServiceProvider provider) =>
        (TestLogger<T>)provider.GetRequiredService<ILogger<T>>();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(TestLogger<>));
        services.AddSingleton<IMeterFactory>(_meterFactory);
        services.AddSingleton(_calls);
        services.AddScoped<ICommandHandler<PipelineCommand>, PipelineCommandHandler>();
        services.AddScoped<ICommandHandler<PipelineCommandWithResponse, Guid>, PipelineCommandWithResponseHandler>();
        services.AddScoped<IQueryHandler<PipelineQuery, int>, PipelineQueryHandler>();
        services.AddScoped<IValidator<PipelineCommand>, PipelineCommandValidator>();
        services.AddScoped<ICommandHandler<OutcomeCommand>, OutcomeCommandHandler>();
        services.AddScoped<IQueryHandler<OutcomeQuery, int>, OutcomeQueryHandler>();
        services.AddScoped<ICommandHandler<ThrowingCommand>, ThrowingCommandHandler>();

        services.AddApplication();

        return services.BuildServiceProvider();
    }

    // Segue o handler interno de cada decorator, guardado no campo do construtor primário.
    private static Type[] Chain(object handler)
    {
        var chain = new List<Type>();
        object? current = handler;

        while (current is not null)
        {
            chain.Add(current.GetType());
            current = current.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(field => IsHandlerType(field.FieldType))
                .Select(field => field.GetValue(current))
                .SingleOrDefault();
        }

        return [.. chain];
    }

    private static bool IsHandlerType(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() is var definition &&
        (definition == typeof(ICommandHandler<>) ||
         definition == typeof(ICommandHandler<,>) ||
         definition == typeof(IQueryHandler<,>));
}

internal sealed class PipelineCalls
{
    public int Count { get; set; }
}

internal sealed record PipelineCommand(string Name) : ICommand;

internal sealed record PipelineCommandWithResponse(string Name) : ICommand<Guid>;

internal sealed record PipelineQuery(int Value) : IQuery<int>;

internal sealed class PipelineCommandValidator : AbstractValidator<PipelineCommand>
{
    public PipelineCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().WithMessage("Informe o nome.");
    }
}

internal sealed class PipelineCommandHandler(PipelineCalls calls) : ICommandHandler<PipelineCommand>
{
    public Task<Result> Handle(PipelineCommand command, CancellationToken cancellationToken)
    {
        calls.Count++;

        return Task.FromResult(Result.Success());
    }
}

internal sealed class PipelineCommandWithResponseHandler : ICommandHandler<PipelineCommandWithResponse, Guid>
{
    public Task<Result<Guid>> Handle(PipelineCommandWithResponse command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(Guid.NewGuid()));
}

internal sealed class PipelineQueryHandler : IQueryHandler<PipelineQuery, int>
{
    public Task<Result<int>> Handle(PipelineQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(query.Value));
}

internal sealed record OutcomeCommand(ErrorType? Failure) : ICommand;

internal sealed record OutcomeQuery(ErrorType? Failure) : IQuery<int>;

internal sealed class OutcomeCommandHandler : ICommandHandler<OutcomeCommand>
{
    public Task<Result> Handle(OutcomeCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(command.Failure is { } type
            ? Result.Failure(new Error("Teste.Saida", "Saída de teste", type))
            : Result.Success());
}

internal sealed class OutcomeQueryHandler : IQueryHandler<OutcomeQuery, int>
{
    public Task<Result<int>> Handle(OutcomeQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(query.Failure is { } type
            ? Result.Failure<int>(new Error("Teste.Saida", "Saída de teste", type))
            : Result.Success(1));
}

internal sealed record ThrowingCommand : ICommand;

internal sealed class ThrowingCommandHandler : ICommandHandler<ThrowingCommand>
{
    public Task<Result> Handle(ThrowingCommand command, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Falha inesperada de teste");
}
