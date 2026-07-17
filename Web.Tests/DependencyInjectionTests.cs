using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Narratum.Web.Tests;

/// <summary>
/// Boots the real application DI container and verifies that every Narratum service
/// the Blazor components inject can actually be resolved. Components inject services
/// by type via @inject; a mismatch (e.g. injecting a concrete type that is only
/// registered behind an interface) compiles fine but throws at render time. This
/// test discovers the injections by reflection, so it covers every component
/// automatically and fails fast for that whole class of bug.
/// </summary>
public class DependencyInjectionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DependencyInjectionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void AllComponentInjectedNarratumServices_AreResolvable()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var componentTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(t => typeof(IComponent).IsAssignableFrom(t) && !t.IsAbstract);

        var unresolved = new List<string>();

        foreach (var componentType in componentTypes)
        {
            var injectedProps = componentType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(p => p.GetCustomAttribute<InjectAttribute>() is not null);

            foreach (var prop in injectedProps)
            {
                // Only assert Narratum's own services. Framework/third-party services
                // (IJSRuntime, NavigationManager, FluentUI) are provided by the render
                // host and are not resolvable from a bare service scope.
                var ns = prop.PropertyType.Namespace ?? string.Empty;
                if (!ns.StartsWith("Narratum", StringComparison.Ordinal))
                    continue;

                if (provider.GetService(prop.PropertyType) is null)
                    unresolved.Add($"{componentType.Name}.{prop.Name} -> {prop.PropertyType.FullName}");
            }
        }

        unresolved.Should().BeEmpty(
            "every Narratum service injected by a component must be registered in DI; " +
            "unresolved: " + string.Join(", ", unresolved));
    }
}
