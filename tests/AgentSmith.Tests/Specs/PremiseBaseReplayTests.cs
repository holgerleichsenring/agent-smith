using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using static AgentSmith.Tests.Specs.SeriesBaseTests;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-04-6768a: a false finding is superseded only on a presence flip between the series base and
/// HEAD in a path a series checkpoint touched; every other outcome leaves it false.
/// </summary>
public sealed class PremiseBaseReplayTests
{
    private const string Repo = DerivationTestLooks.Repo;

    private static readonly PremiseFinding Finding = new(
        "no caller builds its own empty-payload check", "no-longer-holds", "one does", "M1");

    private static (string, int, string) Grep(string at, int exit, string output = "") =>
        ($"grep -InE -e EmptyPayload {at} ", exit, output);

    private static List<(string, int, string)> Series(params (string, int, string)[] answers) =>
        [Origin, Anchor($"rev1\t{Subject} 1 (initial derivation), 2 spec(s)"), ("rev-list", 0, "c2\nc1\n"), .. answers];

    private static async Task<PremiseSupersession?> ReplayAsync(
        ArgvScriptSandbox sandbox, EvidenceRecord record, Action<PipelineContext>? arrange = null, string key = Repo)
    {
        var pipeline = PremiseCheckTestDoubles.Pipeline(PremiseCheckTestDoubles.Draft(), sandbox);
        arrange?.Invoke(pipeline);
        await using var look = DerivationTestLooks.Factory().ForPremiseCheck(pipeline)!;
        var id = look.Evidence.Remember(record with { Repository = key });
        return await PremiseReplayDoubles.Replay().SupersededAsync(
            pipeline, SeriesBaseTests.Set(), look, Finding with { CitedId = id }, CancellationToken.None);
    }

    private static EvidenceRecord Search() =>
        new(Repo, EvidenceRecord.Search, "grep -E 'EmptyPayload' src", 1, true, "src", Pattern: "EmptyPayload");

    private static EvidenceRecord Read(string path) => new(Repo, EvidenceRecord.Read, $"read {path}", 1, true, path);

    [Fact]
    public async Task PremiseBaseReplay_SearchEmptyAtBaseAndMatchedNowByASeriesCommit_IsSuperseded()
    {
        var sandbox = new ArgvScriptSandbox(Series(Grep("c1^", 1), Grep("HEAD", 0, "HEAD:src/Callers.cs:4:EmptyPayload\n"),
            ("log --no-merges", 0, "c2\n")));

        var superseded = await ReplayAsync(sandbox, Search());

        superseded.Should().NotBeNull();
        superseded!.Base.Should().Be("c1^");
        superseded.Paths.Should().Equal("src/Callers.cs");
        superseded.Commits.Should().Equal("c2");
        sandbox.Ran.Should().Contain(a => a.StartsWith("log --no-merges --fixed-strings --grep=[checkpoint] agent-smith run "
            + "--format=%H c1^..HEAD --not origin/main -- src/Callers.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PremiseBaseReplay_GrepExitOneAtTheBase_IsAnEmptyAnswer() =>
        (await ReplayAsync(new ArgvScriptSandbox(Series(Grep("c1^", 1, "noise"), Grep("HEAD", 0, "HEAD:src/A.cs:1:x\n"),
            ("log --no-merges", 0, "c2\n"))), Search())).Should().NotBeNull("exit 1 is grep's 'no match', not a failure");

    [Fact]
    public async Task PremiseBaseReplay_OutputsEqualButForTheRefPrefix_IsNotSuperseded() =>
        (await ReplayAsync(new ArgvScriptSandbox(Series(Grep("c1^", 0, "c1^:src/A.cs:1:x\n"), Grep("HEAD", 0, "HEAD:src/A.cs:1:x\n"),
            ("log --no-merges", 0, "c2\n"))), Search())).Should().BeNull();

    [Fact]
    public async Task PremiseBaseReplay_SearchSameAtBaseAndNow_IsNotSuperseded() =>
        (await ReplayAsync(new ArgvScriptSandbox(Series(Grep("c1^", 1), Grep("HEAD", 1), ("log --no-merges", 0, "c2\n"))),
            Search())).Should().BeNull();

    [Fact]
    public async Task PremiseBaseReplay_SearchMatchedAtBothWithMoreMatchesNow_IsNotSuperseded() =>
        (await ReplayAsync(new ArgvScriptSandbox(Series(Grep("c1^", 0, "c1^:src/A.cs:1:x\n"),
            Grep("HEAD", 0, "HEAD:src/A.cs:1:x\nHEAD:src/A.cs:9:x\n"), ("log --no-merges", 0, "c2\n"))), Search()))
            .Should().BeNull("more matches in a path present at both is a content change, not a flip");

    [Fact]
    public async Task PremiseBaseReplay_DifferenceOnlyFromAMergedBaseCommit_IsNotSuperseded()
    {
        var sandbox = new ArgvScriptSandbox(Series(Grep("c1^", 1), Grep("HEAD", 0, "HEAD:src/A.cs:1:x\n"), ("log --no-merges", 0, "")));

        (await ReplayAsync(sandbox, Search())).Should().BeNull("no series checkpoint touched the flipped path");
    }

    [Fact]
    public async Task PremiseBaseReplay_MarkedCommitMergedFromTheBase_IsNotSuperseded()
    {
        var sandbox = new ArgvScriptSandbox(Series(Grep("c1^", 1), Grep("HEAD", 0, "HEAD:src/A.cs:1:x\n"), ("log --no-merges", 0, "")));

        (await ReplayAsync(sandbox, Search())).Should().BeNull();
        sandbox.Ran.Single(a => a.StartsWith("log --no-merges", StringComparison.Ordinal)).Should().Contain("--not origin/main",
            "a checkpoint another ticket's run made reaches this branch only through the base, which git excludes");
    }

    [Fact]
    public async Task PremiseBaseReplay_ReadOfAFileASeriesCommitCreated_IsSuperseded()
    {
        var sandbox = new ArgvScriptSandbox(Series(("ls-tree --name-only c1^ ", 0, ""),
            ("ls-tree --name-only HEAD ", 0, "src/New.cs\n"), ("log --no-merges", 0, "c2\n")));

        (await ReplayAsync(sandbox, Read("src/New.cs")))!.Paths.Should().Equal("src/New.cs");
    }

    [Fact]
    public async Task PremiseBaseReplay_ReadOfAFileChangedButPresentAtBoth_IsNotSuperseded() =>
        (await ReplayAsync(new ArgvScriptSandbox(Series(("ls-tree --name-only c1^ ", 0, "src/Old.cs\n"),
            ("ls-tree --name-only HEAD ", 0, "src/Old.cs\n"), ("log --no-merges", 0, "c2\n"))), Read("src/Old.cs")))
            .Should().BeNull();

    [Fact]
    public async Task PremiseBaseReplay_AuditLook_IsNotSuperseded()
    {
        var sandbox = new ArgvScriptSandbox(Series());

        (await ReplayAsync(sandbox, new EvidenceRecord(Repo, EvidenceRecord.Audit, "npm audit", 0, true))).Should().BeNull();
        sandbox.Ran.Should().BeEmpty("a look of another kind has no replay");
    }

    [Fact]
    public async Task PremiseBaseReplay_GitStepFails_IsNotSuperseded() =>
        (await ReplayAsync(new ArgvScriptSandbox(Series(Grep("c1^", 1), Grep("HEAD", 2), ("log --no-merges", 0, "c2\n"))),
            Search())).Should().BeNull();

    [Fact]
    public async Task PremiseBaseReplay_ACheckpointOnlyInASecondarySandbox_ReplaysOnThePrimary()
    {
        var primary = new ArgvScriptSandbox(Series(Grep("c1^", 1), Grep("HEAD", 0, "HEAD:src/A.cs:1:x\n"),
            ("log --no-merges", 0, "c2\n")));
        var secondary = new ArgvScriptSandbox([]);

        var superseded = await ReplayAsync(primary, Search(), pipeline =>
        {
            pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)new Dictionary<string, ISandbox>
                { ["repo/api"] = primary, ["repo/web"] = secondary });
            pipeline.Set(ContextKeys.SandboxRepos, (IReadOnlyDictionary<string, string>)new Dictionary<string, string>
                { ["repo/api"] = "repo", ["repo/web"] = "repo" });
            pipeline.Set(ContextKeys.Repos, (IReadOnlyList<RepoConnection>)[new RepoConnection { Name = "repo" }]);
        }, key: "repo/web");

        superseded.Should().NotBeNull();
        secondary.Ran.Should().BeEmpty("checkpoint commits land only in a repository's first sandbox");
    }
}
