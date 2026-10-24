using FluentValidation;
using TickestPristine.Application.Abstractions.Messaging;

namespace TickestPristine.ArchitectureTests.Conventions.Fixtures;

// Tipos de exemplo dos testes do CommandValidatorChecker: nunca são registrados nem executados.

internal sealed record CommandWithValidator : ICommand;

internal sealed class CommandWithValidatorValidator : AbstractValidator<CommandWithValidator>;

internal sealed record CommandWithResponseAndValidator : ICommand<int>;

internal sealed class CommandWithResponseAndValidatorValidator : AbstractValidator<CommandWithResponseAndValidator>;

internal sealed record CommandWithIndirectValidator : ICommand;

internal abstract class FixtureBaseValidator<T> : AbstractValidator<T>;

internal sealed class CommandWithIndirectValidatorValidator : FixtureBaseValidator<CommandWithIndirectValidator>;

internal sealed record CommandWithoutValidator : ICommand;

internal sealed record CommandWithForeignValidator : ICommand;

// Validator com o nome do command, mas que valida outro command.
internal sealed class CommandWithForeignValidatorValidator : AbstractValidator<CommandWithValidator>;
