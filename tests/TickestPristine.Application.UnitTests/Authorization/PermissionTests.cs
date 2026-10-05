using System.Reflection;
using TickestPristine.Domain.Roles;

namespace TickestPristine.Application.UnitTests.Authorization;

public sealed class PermissionTests
{
    [Fact]
    public void All_Should_HaveUniqueCodes()
    {
        Permission.All.Select(p => p.Code).ShouldBeUnique();
    }

    [Fact]
    public void All_Should_ContainEveryPermissionCodeConstant()
    {
        var constants = typeof(PermissionCodes)
            .GetNestedTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Permission.All.Select(p => p.Code).ShouldBe(constants, ignoreOrder: true);
    }

    [Theory]
    [InlineData(PermissionCodes.Roles.Manage, true)]
    [InlineData(PermissionCodes.Tickets.Create, false)]
    public void Find_Should_ReturnAdministrativeFlagFromCatalog(string code, bool expected)
    {
        Permission.Find(code)!.IsAdministrative.ShouldBe(expected);
    }

    [Fact]
    public void GetDisplayName_Should_ReturnCode_WhenCodeIsNotInCatalog()
    {
        Permission.GetDisplayName("legacy:unknown").ShouldBe("legacy:unknown");
    }
}
