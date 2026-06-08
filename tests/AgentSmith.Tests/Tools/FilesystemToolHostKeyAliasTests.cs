using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// p0250: one denominator for sandbox addressing. The master addresses repos by
/// NAME; the tool host aliases each name to its sandbox via the authoritative
/// key→repo map, so a write to a MULTI-GROUP repo (sandbox keyed
/// "&lt;repo&gt;-&lt;langSlug&gt;") lands in that repo's sandbox — the SAME one
/// CommitAndPR's repo-name lookup commits from. Before p0250 the agent addressed
/// by the composite KEY while the commit resolved by NAME, and they diverged for
/// any multi-group repo, dropping the source change at commit time.
/// </summary>
public sealed class FilesystemToolHostKeyAliasTests
{
    private static Mock<ISandbox> NewSandbox()
    {
        var m = new Mock<ISandbox>();
        m.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null)));
        return m;
    }

    private static (FilesystemToolHost Host, Mock<ISandbox> Server, Mock<ISandbox> Docs) Build()
    {
        var server = NewSandbox();   // multi-group repo → key "svc-server-csharp"
        var docs = NewSandbox();     // single-group repo → key "svc-docs"
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["svc-server-csharp"] = server.Object,
            ["svc-docs"] = docs.Object,
        };
        var keyToRepo = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["svc-server-csharp"] = "svc-server",
            ["svc-docs"] = "svc-docs",
        };
        var host = new FilesystemToolHost(sandboxes, defaultRepo: "svc-server-csharp", keyToRepo: keyToRepo);
        return (host, server, docs);
    }

    [Fact]
    public async Task MultiGroupRepo_AddressedByRepoName_DispatchesToItsSandbox()
    {
        var (host, server, docs) = Build();

        // The master addresses by REPO NAME (svc-server), not the toolchain key.
        await host.ReadFile("svc-server/src/Controllers/X.cs");

        server.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        docs.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompositeKeyPrefix_StillResolves_AliasIsAdditive()
    {
        var (host, server, _) = Build();

        // Back-compat: addressing by the full composite key still routes correctly.
        await host.ReadFile("svc-server-csharp/src/X.cs");

        server.Verify(s => s.RunStepAsync(
            It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
