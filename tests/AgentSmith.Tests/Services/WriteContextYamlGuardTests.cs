using System.Text.Json;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

// p0341c: write_context_yaml constrains context_name to the repo's DISCOVERED contexts —
// the invariant belongs in the write API, not the prompt. An invented name (e.g. the
// example 'default') when real contexts exist is rejected; a genuine bootstrap (no
// discovery) is unaffected.
public sealed class WriteContextYamlGuardTests
{
    private static readonly JsonElement EmptyDoc = JsonDocument.Parse("{}").RootElement;
    private const string GuardMarker = "is not a discovered context";

    private static WriteContextYamlToolHost Host(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? discovered, string? defaultRepoName = null) =>
        new(
            new Dictionary<string, ISandbox>(),
            defaultRepo: "repo",
            serializer: new AgentSmith.Infrastructure.Services.ContextYamlSerializer(),
            discoveredContexts: discovered,
            defaultRepoName: defaultRepoName);

    [Fact]
    public async Task ContextNameGuard_InventedDefaultWhenApiExists_Rejected()
    {
        var host = Host(
            new Dictionary<string, IReadOnlyList<string>> { ["api-repo"] = new[] { "api", "worker" } },
            defaultRepoName: "api-repo");

        var result = await host.WriteContextYaml("api-repo", "default", EmptyDoc);

        result.Should().Contain(GuardMarker);
        result.Should().Contain("api");
    }

    [Fact]
    public async Task ContextNameGuard_DiscoveredKey_Accepted()
    {
        var host = Host(
            new Dictionary<string, IReadOnlyList<string>> { ["api-repo"] = new[] { "api", "worker" } },
            defaultRepoName: "api-repo");

        // 'api' is a real discovered context — the guard passes; the write then fails later
        // on the empty document's missing meta.workdir (proving the guard was cleared).
        var result = await host.WriteContextYaml("api-repo", "api", EmptyDoc);

        result.Should().NotContain(GuardMarker);
    }

    [Fact]
    public async Task ContextNameGuard_SingleDiscovered_InventedName_RedirectedNotRejected()
    {
        var host = Host(
            new Dictionary<string, IReadOnlyList<string>> { ["api-repo"] = new[] { "api" } },
            defaultRepoName: "api-repo");

        var result = await host.WriteContextYaml("api-repo", "default", EmptyDoc);

        result.Should().NotContain(GuardMarker, "a single discovered context redirects rather than rejects");
    }

    [Fact]
    public async Task ContextNameGuard_SyntheticDefaultOnlyBootstrap_Accepted()
    {
        // No discovery at all => genuine bootstrap, any name allowed (the guard is a no-op).
        var host = Host(discovered: null);

        var result = await host.WriteContextYaml("", "default", EmptyDoc);

        result.Should().NotContain(GuardMarker);
    }
}
