using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ProviderRegistryTests
{
    private sealed class FakeProvider(string providerType) : ITypedProvider
    {
        public string ProviderType => providerType;
    }

    [Fact]
    public void Resolve_returns_matching_provider()
    {
        var github = new FakeProvider("github");
        var gitlab = new FakeProvider("gitlab");
        var registry = new ProviderRegistry<ITypedProvider>([github, gitlab]);

        registry.Resolve("github").Should().BeSameAs(github);
        registry.Resolve("gitlab").Should().BeSameAs(gitlab);
    }

    [Fact]
    public void Resolve_is_case_insensitive()
    {
        var provider = new FakeProvider("GitHub");
        var registry = new ProviderRegistry<ITypedProvider>([provider]);

        registry.Resolve("github").Should().BeSameAs(provider);
        registry.Resolve("GITHUB").Should().BeSameAs(provider);
    }

    [Fact]
    public void Resolve_throws_for_unknown_type()
    {
        var registry = new ProviderRegistry<ITypedProvider>([new FakeProvider("github")]);

        var act = () => registry.Resolve("jira");

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*jira*")
            .WithMessage("*github*");
    }

    [Fact]
    public void Constructor_throws_on_duplicate_provider_type()
    {
        var act = () => new ProviderRegistry<ITypedProvider>(
            [new FakeProvider("github"), new FakeProvider("github")]);

        act.Should().Throw<ConfigurationException>()
            .WithMessage("*Duplicate*github*");
    }

    [Fact]
    public void TryResolve_returns_true_for_existing_provider()
    {
        var provider = new FakeProvider("github");
        var registry = new ProviderRegistry<ITypedProvider>([provider]);

        registry.TryResolve("github", out var result).Should().BeTrue();
        result.Should().BeSameAs(provider);
    }

    [Fact]
    public void TryResolve_returns_false_for_unknown_type()
    {
        var registry = new ProviderRegistry<ITypedProvider>([new FakeProvider("github")]);

        registry.TryResolve("jira", out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void RegisteredTypes_lists_all_types()
    {
        var registry = new ProviderRegistry<ITypedProvider>(
            [new FakeProvider("github"), new FakeProvider("gitlab")]);

        registry.RegisteredTypes.Should().BeEquivalentTo(["github", "gitlab"]);
    }

    [Fact]
    public void Empty_registry_has_no_types()
    {
        var registry = new ProviderRegistry<ITypedProvider>([]);

        registry.RegisteredTypes.Should().BeEmpty();
    }
}
