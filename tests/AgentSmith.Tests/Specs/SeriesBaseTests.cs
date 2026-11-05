using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-04-6768a: the series base read off the branch — the first parent of the oldest checkpoint the
/// series made after its current text was cut, with merged base commits left out by git itself.
/// </summary>
public sealed class SeriesBaseTests
{
    internal const string SeriesId = "2026-11-04-0a1b";
    internal const string Subject = $"spec: {SeriesId} (azdo-1) revision";

    internal static SpecSet Set() => new("azdo-1", [], SpecAccounting.Empty, [], SpecSource.Derived, Series: SeriesId);

    internal static (string, int, string) Anchor(params string[] revisions) =>
        ("log --format=%H%x09%s", 0, string.Join('\n', revisions) + "\n");

    internal static (string, int, string) Origin => ("symbolic-ref", 0, "origin/main\n");

    private static Task<SeriesStart?> ResolveAsync(ArgvScriptSandbox sandbox) =>
        PremiseReplayDoubles.SeriesBase().ResolveAsync(sandbox, Set(), null, CancellationToken.None);

    [Fact]
    public async Task SeriesBase_CheckpointCommitsOnTheBranch_IsTheOldestOnesParent()
    {
        var sandbox = new ArgvScriptSandbox([Origin, Anchor($"rev1\t{Subject} 1 (initial derivation), 2 spec(s)"),
            ("rev-list", 0, "c2\nc1\n")]);

        var start = await ResolveAsync(sandbox);

        start.Should().Be(new SeriesStart("c1^", "origin/main"));
        sandbox.Ran.Should().Contain(
            "rev-list --no-merges --fixed-strings --grep=[checkpoint] agent-smith run HEAD --not origin/main rev1");
    }

    [Fact]
    public async Task SeriesBase_NoCheckpointCommit_IsHead()
    {
        var sandbox = new ArgvScriptSandbox([Origin, Anchor($"rev1\t{Subject} 1 (initial derivation), 2 spec(s)"),
            ("rev-list", 0, "")]);

        (await ResolveAsync(sandbox))!.IsHead.Should().BeTrue();
    }

    [Fact]
    public async Task SeriesBase_CommitsMergedFromTheBase_AreNotTheSeries()
    {
        var sandbox = new ArgvScriptSandbox([Origin, Anchor($"rev1\t{Subject} 1 (initial derivation), 1 spec(s)"),
            ("rev-list", 0, "c1\n")]);

        await ResolveAsync(sandbox);

        sandbox.Ran.Single(a => a.StartsWith("rev-list", StringComparison.Ordinal))
            .Should().Contain("--no-merges").And.Contain("--not origin/main",
                "a merge and a fast-forwarded base commit are both the base's, never the series'");
    }

    [Fact]
    public async Task SeriesBase_CheckpointsBeforeTheCurrentRevision_AreNotTheSeries()
    {
        var sandbox = new ArgvScriptSandbox([Origin,
            Anchor($"rev2\t{Subject} 2 (comment on the ticket), 2 spec(s)", $"rev1\t{Subject} 1 (initial derivation), 2 spec(s)"),
            ("rev-list", 0, "c3\n")]);

        var start = await ResolveAsync(sandbox);

        start!.Commit.Should().Be("c3^");
        sandbox.Ran.Single(a => a.StartsWith("rev-list", StringComparison.Ordinal)).Should().EndWith(" rev2",
            "a re-cut writes its facts against what earlier runs left, so their checkpoints are not its series work");
    }

    [Fact]
    public async Task SeriesBase_ACitationRegroundRevision_IsNotTheAnchor()
    {
        var sandbox = new ArgvScriptSandbox([Origin,
            Anchor($"rev3\t{Subject} 3 (citations re-grounded in 2026-11-04-0a1bb: a -> b), 2 spec(s)",
                $"rev1\t{Subject} 1 (initial derivation), 2 spec(s)"),
            ("rev-list", 0, "c2\nc1\n")]);

        await ResolveAsync(sandbox);

        sandbox.Ran.Single(a => a.StartsWith("rev-list", StringComparison.Ordinal)).Should().EndWith(" rev1");
    }

    [Fact]
    public async Task SeriesBase_NoRevisionOrUnreadableHistory_IsNull()
    {
        (await ResolveAsync(new ArgvScriptSandbox([Origin, Anchor("other\tspec: another (x) revision 1 (resume), 1 spec(s)")])))
            .Should().BeNull();
        (await ResolveAsync(new ArgvScriptSandbox([Origin, Anchor($"rev1\t{Subject} 1 (resume), 1 spec(s)"),
            ("rev-list", 128, "")]))).Should().BeNull();
    }
}
