using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// p0249: repo→sandbox resolution. The live bug: a multi-repo run where a repo is
/// MULTI-GROUP gets the SandboxKeyComposer key "&lt;repo&gt;-&lt;langSlug&gt;", but
/// SandboxesForRepo only decoded "&lt;repo&gt;" / "&lt;repo&gt;/..." — so the repo's
/// sandbox was never found, CommitAndPR saw "no staged changes", and a real source
/// edit was dropped (keystone FAILED). Resolution is now authoritative via the
/// coordinator's key→repo map, with a key-decode fallback that covers every form.
/// </summary>
public sealed class SandboxTargetsTests
{
    private static PipelineContext TwoRepoContext(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        IReadOnlyDictionary<string, string>? owners)
    {
        var ctx = new PipelineContext();
        ctx.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = "svc-server" }, new RepoConnection { Name = "svc-client" }]);
        ctx.Set(ContextKeys.Sandboxes, sandboxes);
        if (owners is not null) ctx.Set(ContextKeys.SandboxRepos, owners);
        return ctx;
    }

    [Fact]
    public void MultiGroupKey_ResolvesToItsRepo_ViaAuthoritativeMap()
    {
        // "svc-server-csharp" is the multi-group key the OLD matcher missed entirely.
        var server = new StubSandbox();
        var sandboxes = new Dictionary<string, ISandbox>
        {
            ["svc-server-csharp"] = server,
            ["svc-client"] = new StubSandbox(),
        };
        var owners = new Dictionary<string, string>
        {
            ["svc-server-csharp"] = "svc-server",
            ["svc-client"] = "svc-client",
        };
        var ctx = TwoRepoContext(sandboxes, owners);

        var match = SandboxTargets.SandboxesForRepo(ctx, new RepoConnection { Name = "svc-server" });

        match.Should().ContainSingle().Which.Value.Should().BeSameAs(server,
            "the multi-group <repo>-<langSlug> sandbox must resolve to its repo");
    }

    [Fact]
    public void MultiGroupKey_ResolvesToItsRepo_ViaKeyDecodeFallback_WhenNoMap()
    {
        // No SandboxRepos map (older context): the key-decode fallback must still
        // cover the "<repo>-..." form that the original code dropped.
        var server = new StubSandbox();
        var sandboxes = new Dictionary<string, ISandbox>
        {
            ["svc-server-csharp"] = server,
            ["svc-client"] = new StubSandbox(),
        };
        var ctx = TwoRepoContext(sandboxes, owners: null);

        var match = SandboxTargets.SandboxesForRepo(ctx, new RepoConnection { Name = "svc-server" });

        match.Should().ContainSingle().Which.Value.Should().BeSameAs(server);
    }

    [Fact]
    public void SingleGroupAndMonorepoForms_StillResolve()
    {
        var exact = new StubSandbox();
        var ctxScoped = new StubSandbox();
        var sandboxes = new Dictionary<string, ISandbox>
        {
            ["svc-server"] = exact,          // multi-repo single-group
            ["svc-client/api"] = ctxScoped,  // multi-repo monorepo context
        };
        var owners = new Dictionary<string, string>
        {
            ["svc-server"] = "svc-server",
            ["svc-client/api"] = "svc-client",
        };
        var ctx = TwoRepoContext(sandboxes, owners);

        SandboxTargets.SandboxesForRepo(ctx, new RepoConnection { Name = "svc-server" })
            .Should().ContainSingle().Which.Value.Should().BeSameAs(exact);
        SandboxTargets.SandboxesForRepo(ctx, new RepoConnection { Name = "svc-client" })
            .Should().ContainSingle().Which.Value.Should().BeSameAs(ctxScoped);
    }
}
