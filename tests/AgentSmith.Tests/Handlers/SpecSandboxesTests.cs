using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>2026-10-17-a196a: a spec's own checks run over the sandboxes of the repositories it names.</summary>
public sealed class SpecSandboxesTests
{
    private static PipelineContext Run(IReadOnlyList<string>? specRepos)
    {
        var ctx = new PipelineContext();
        ctx.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "Sample.Server" }, new RepoConnection { Name = "Sample.Worker" }]);
        ctx.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox>
        {
            ["Sample.Server-csharp"] = new StubSandbox(), ["Sample.Server-node"] = new StubSandbox(),
            ["Sample.Worker"] = new StubSandbox(),
        });
        ctx.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>());
        ctx.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string>
        {
            ["Sample.Server-csharp"] = "Sample.Server", ["Sample.Server-node"] = "Sample.Server",
            ["Sample.Worker"] = "Sample.Worker",
        });
        if (specRepos is not null)
            ctx.Set(ContextKeys.CurrentSpec, new SpecDraft("p1", "g", "spec: p1", []) { Repos = specRepos });
        return ctx;
    }

    private static SpecSandboxes Resolver() => new(new SandboxTargets());

    [Fact]
    public void SpecSandboxes_SpecNamingOneRepository_ResolvesOnlyItsSandboxes()
    {
        Resolver().TryResolve(Run(["sample.server"]), out var sandboxes, out var unresolved).Should().BeTrue();

        unresolved.Should().BeNull();
        sandboxes.Keys.Should().BeEquivalentTo("Sample.Server-csharp", "Sample.Server-node");
    }

    [Fact]
    public void SpecSandboxes_SpecNamingNone_KeepsEverySandbox()
    {
        Resolver().TryResolve(Run([]), out var named, out _).Should().BeTrue();
        Resolver().TryResolve(Run(null), out var noSpec, out _).Should().BeTrue();

        named.Should().HaveCount(3);
        noSpec.Should().HaveCount(3);
    }

    [Fact]
    public void SpecSandboxes_NamedRepositoryWithoutSandbox_IsUnresolved()
    {
        Resolver().TryResolve(Run(["Sample.Billing"]), out var sandboxes, out var unresolved).Should().BeTrue();

        sandboxes.Should().BeEmpty("another repository is never checked in its place");
        unresolved.Should().Contain("Spec p1 names Sample.Billing");
    }

    [Fact]
    public void SpecSandboxes_RunWithoutSandboxes_IsFalse() =>
        Resolver().TryResolve(new PipelineContext(), out _, out _).Should().BeFalse();
}
