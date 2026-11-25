using AgentSmith.Application.Services.BaseRefresh;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-24-6811c: the merge a master resolved is built by the repository's declared stages before the push —
/// over the levelled tree, so the stages build what would be pushed. 2026-11-25-9992b: every key of a repository works in
/// the workspace's one tree, so nothing is levelled into a second clone any more.</summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class BaseMergeVerifyTests
{
    [Fact]
    public async Task AResolvedConflict_ItsStagesRunOverTheLevelledTree()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        var primary = clones.Primary;
        await clones.GitAsync(primary, "checkout", "-q", "-b", "main");
        await clones.WriteAsync(primary, "web/app.ts", "the base's\n");
        await clones.CommitAllAsync(primary, "base");
        await clones.GitAsync(primary, "push", "-q", "origin", "main");
        await clones.GitAsync(primary, "checkout", "-q", SandboxClones.Branch);
        await clones.WriteAsync(primary, "web/app.ts", "the branch's\n");
        await clones.CommitAllAsync(primary, "branch");
        await clones.GitAsync(primary, "push", "-q", "origin", SandboxClones.Branch);
        await clones.GitAsync(primary, "fetch", "-q", "origin");
        await GitMayFail(clones, primary, "merge", "--no-edit", "origin/main");
        await clones.WriteAsync(primary, "web/app.ts", "both kept\n");
        await clones.CommitAllAsync(primary, "merge");
        await clones.WriteAsync(primary, "stray.txt", "left over\n");
        Stages(clones, primary, "grep -q 'both kept' web/app.ts && test ! -e stray.txt");

        var verdict = await Verify().RunAsync(clones.Pipeline, Repo(clones), CancellationToken.None);

        verdict.Should().Be(new BaseMergeVerdict(1, null, null));
    }

    private static async Task GitMayFail(SandboxClones clones, string key, params string[] args)
    {
        var sandbox = clones.Sandboxes[key];
        await new BaseRefreshGit().RunAsync(sandbox, args, CancellationToken.None);
    }

    private static RepoConnection Repo(SandboxClones clones) =>
        clones.Pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)[0];

    private static void Stages(SandboxClones clones, string key, string command) =>
        clones.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                [key] = [new RemoteContextDiscovery(key, ".", "csharp", Verify: [new("build", command)])],
            });

    private static BaseMergeVerify Verify() => new(new ContextVerifyStagesResolver(),
        new VerifyStageResolver(new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance), new SandboxTargets(), new BaseRefreshGit(),
        NullLogger<BaseMergeVerify>.Instance);
}
