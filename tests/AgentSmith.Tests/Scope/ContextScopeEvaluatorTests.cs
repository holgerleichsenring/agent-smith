using AgentSmith.Application.Services.Scope;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Scope;

/// <summary>
/// p0336b: context-level scoping narrows within a kept repo. Only a confident,
/// fully-valid STRICT subset drops contexts; every doubtful path keeps all
/// contexts of the repo (a wrong verdict must never shed a needed sandbox).
/// </summary>
public sealed class ContextScopeEvaluatorTests
{
    private static readonly IReadOnlyList<RepoConnection> Server = [new() { Name = "server" }];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>> Inventory =
        new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>
        {
            ["server"] =
            [
                new("sdk8", ".", "csharp"), new("sdk9", ".", "csharp"), new("encrypter", ".", "csharp"),
            ],
        };

    [Fact]
    public void ContextScope_ConfidentStrictSubset_DropsTheRest()
    {
        var (scoped, dropped) = ContextScopeEvaluator.Evaluate(
            Classification(0.9, ("server", ["sdk8", "sdk9"])), null, Server, Inventory);

        scoped.Should().NotBeNull();
        scoped!["server"].Should().BeEquivalentTo("sdk8", "sdk9");
        dropped.Should().ContainSingle(d => d.Repo == "server" && d.Context == "encrypter");
    }

    [Fact]
    public void ContextScope_LowConfidence_KeepsAllContexts()
    {
        var (scoped, dropped) = ContextScopeEvaluator.Evaluate(
            Classification(0.5, ("server", ["sdk8"])), null, Server, Inventory);

        scoped.Should().BeNull();
        dropped.Should().BeEmpty();
    }

    [Fact]
    public void ContextScope_NoContextVerdict_KeepsAllContexts()
    {
        var (scoped, _) = ContextScopeEvaluator.Evaluate(
            new RepoScopeClassification(["server"], 0.9, null, Contexts: null), null, Server, Inventory);

        scoped.Should().BeNull();
    }

    [Fact]
    public void ContextScope_UnknownContextNamed_KeepsAllContexts()
    {
        var (scoped, _) = ContextScopeEvaluator.Evaluate(
            Classification(0.9, ("server", ["sdk8", "bogus"])), null, Server, Inventory);

        scoped.Should().BeNull("an unknown context name distrusts the whole repo verdict");
    }

    [Fact]
    public void ContextScope_FullSet_KeepsAllContexts_NoNarrowing()
    {
        var (scoped, dropped) = ContextScopeEvaluator.Evaluate(
            Classification(0.9, ("server", ["sdk8", "sdk9", "encrypter"])), null, Server, Inventory);

        scoped.Should().BeNull();
        dropped.Should().BeEmpty();
    }

    [Fact]
    public void ContextScope_ClassifierError_KeepsAllContexts()
    {
        var (scoped, _) = ContextScopeEvaluator.Evaluate(
            Classification(0.9, ("server", ["sdk8"])), "call failed", Server, Inventory);

        scoped.Should().BeNull();
    }

    private static RepoScopeClassification Classification(
        double confidence, params (string Repo, IReadOnlyList<string> Contexts)[] contexts) =>
        new(["server"], confidence, null,
            contexts.ToDictionary(c => c.Repo, c => c.Contexts, StringComparer.OrdinalIgnoreCase));
}
