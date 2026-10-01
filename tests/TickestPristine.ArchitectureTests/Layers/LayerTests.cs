using NetArchTest.Rules;
using Shouldly;

namespace TickestPristine.ArchitectureTests.Layers;

public class LayerTests : BaseTest
{
    [Fact]
    public void SharedKernelLayer_Should_NotHaveDependencyOnDomainLayer()
    {
        // Arrange
        var types = Types.InAssembly(SharedKernelAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(DomainAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void SharedKernelLayer_Should_NotHaveDependencyOnApplicationLayer()
    {
        // Arrange
        var types = Types.InAssembly(SharedKernelAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(ApplicationAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void SharedKernelLayer_Should_NotHaveDependencyOnInfrastructureLayer()
    {
        // Arrange
        var types = Types.InAssembly(SharedKernelAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void SharedKernelLayer_Should_NotHaveDependencyOnPresentationLayer()
    {
        // Arrange
        var types = Types.InAssembly(SharedKernelAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void DomainLayer_Should_NotHaveDependencyOnApplicationLayer()
    {
        // Arrange
        var types = Types.InAssembly(DomainAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(ApplicationAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void DomainLayer_Should_NotHaveDependencyOnInfrastructureLayer()
    {
        // Arrange
        var types = Types.InAssembly(DomainAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void DomainLayer_Should_NotHaveDependencyOnPresentationLayer()
    {
        // Arrange
        var types = Types.InAssembly(DomainAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void ApplicationLayer_Should_NotHaveDependencyOnInfrastructureLayer()
    {
        // Arrange
        var types = Types.InAssembly(ApplicationAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void ApplicationLayer_Should_NotHaveDependencyOnPresentationLayer()
    {
        // Arrange
        var types = Types.InAssembly(ApplicationAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public void InfrastructureLayer_Should_NotHaveDependencyOnPresentationLayer()
    {
        // Arrange
        var types = Types.InAssembly(InfrastructureAssembly);

        // Act
        TestResult result = types
            .Should()
            .NotHaveDependencyOn(PresentationAssembly.GetName().Name)
            .GetResult();

        // Assert
        result.IsSuccessful.ShouldBeTrue();
    }
}
