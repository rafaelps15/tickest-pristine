using System.Reflection;
using Shouldly;
using TickestPristine.Domain.Tickets;

namespace TickestPristine.ArchitectureTests.Aggregates;

public class TicketSlaAggregateTests : BaseTest
{
    private const BindingFlags PublicDeclared =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Ticket_Should_NotExposeTimerParameters()
    {
        // Arrange
        MethodInfo[] methods = PublicMethods(typeof(Ticket));

        // Act
        string[] receivers =
        [
            .. methods
            .Where(m => m.GetParameters().Any(p => Mentions(p.ParameterType, typeof(TicketSlaTimer))))
                .Select(m => m.Name)
                .Distinct()
        ];

        // Assert
        receivers.ShouldBeEmpty();
    }

    [Fact]
    public void TicketSlaTimer_Should_ExposeOnlyTheApprovedPublicMembers()
    {
        // Arrange
        Type timer = typeof(TicketSlaTimer);

        // Act
        string[] methods = [.. PublicMethods(timer).Select(m => m.Name).Order(StringComparer.Ordinal)];
        string[] computedProperties =
        [
            .. timer.GetProperties(PublicDeclared).Where(p => p.SetMethod is null).Select(p => p.Name)
        ];

        // Assert
        methods.ShouldBe(
        [
            nameof(TicketSlaTimer.ConsumedBusinessTime),
            nameof(TicketSlaTimer.IsDueForMonitoring),
            nameof(TicketSlaTimer.StateAt)
        ]);
        computedProperties.ShouldBe([nameof(TicketSlaTimer.IsOpen)]);
    }

    private static MethodInfo[] PublicMethods(Type type) =>
        [.. type.GetMethods(PublicDeclared).Where(m => !m.IsSpecialName)];

    private static bool Mentions(Type type, Type target) =>
        type == target ||
        type.HasElementType && Mentions(type.GetElementType()!, target) ||
        type.IsGenericType && type.GetGenericArguments().Any(argument => Mentions(argument, target));
}
