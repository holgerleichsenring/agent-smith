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

/// <summary>2026-11-24-6811c: a repository whose contexts need two images is two sandboxes; the merge a master resolved in
/// the primary reaches the secondary as the same tree — through the shared merge-tree and the resolution's diff alone, never
/// the landed base as a diff — so each context's stages build what would be pushed.</summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class BaseMergeVerifyTests
{
    [Fact]
    public async Task AResolvedConflict_ReachesTheSecondary_AndItsStagesRunThere()
    {
        await using var clones = await SandboxClones.CreateAsync();
        if (clones is null) return;
        var (primary, secondary) = (clones.Primary, clones.Secondary);
        await clones.GitAsync(primary, "checkout", "-q", "-b", "main");
        await clones.WriteAsync(primary, "web/app.ts", "the base's\n");
        await clones.CommitAllAsync(primary, "base");
        await clones.GitAsync(primary, "push", "-q", "origin", "main");
        await clones.GitAsync(primary, "checkout", "-q", SandboxClones.Branch);
        await clones.WriteAsync(primary, "web/app.ts", "the branch's\n");
        await clones.CommitAllAsync(primary, "branch");
        await clones.GitAsync(primary, "push", "-q", "origin", SandboxClones.Branch);
        await clones.GitAsync(secondary, "fetch", "-q", "origin");
        await clones.GitAsync(secondary, "reset", "-q", "--hard", $"origin/{SandboxClones.Branch}");
        var head = await clones.HeadAsync(primary);
        await clones.GitAsync(primary, "fetch", "-q", "origin");
        await GitMayFail(clones, primary, "merge", "--no-edit", "origin/main");
        await clones.WriteAsync(primary, "web/app.ts", "both kept\n");
        await clones.CommitAllAsync(primary, "merge");
        Stages(clones, "grep -q 'both kept' web/app.ts");

        var verdict = await Verify().RunAsync(clones.Pipeline, Repo(clones), head, "origin/main", CancellationToken.None);

        verdict.Should().Be(new BaseMergeVerdict(2, null, null));
        (await clones.ReadAsync(secondary, "web/app.ts")).Should().Be("both kept\n");
    }

    private static async Task GitMayFail(SandboxClones clones, string key, params string[] args)
    {
        var sandbox = clones.Sandboxes[key];
        await new BaseRefreshGit().RunAsync(sandbox, args, CancellationToken.None);
    }

    private static RepoConnection Repo(SandboxClones clones) =>
        clones.Pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos)[0];

    private static void Stages(SandboxClones clones, string command) =>
        clones.Pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(ContextKeys.SandboxContexts,
            clones.Sandboxes.Keys.ToDictionary(k => k, k => (IReadOnlyList<RemoteContextDiscovery>)
                [new RemoteContextDiscovery(k, ".", "csharp", Verify: [new("build", command)])], StringComparer.Ordinal));

    private static BaseMergeVerify Verify() => new(new ContextVerifyStagesResolver(),
        new VerifyStageResolver(new DeclaredStagePresence(new SandboxFileReaderFactory(), NullLogger<DeclaredStagePresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance),
        new VerifyCommandRunner(NullLogger<VerifyCommandRunner>.Instance), new SandboxTargets(), TestTreeSync.TreeGit(), new BaseRefreshGit(),
        NullLogger<BaseMergeVerify>.Instance);
}
