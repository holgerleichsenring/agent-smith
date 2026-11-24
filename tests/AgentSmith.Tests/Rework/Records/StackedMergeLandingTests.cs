using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Rework;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-22-6d85b: a pull request merged into a prerequisite's ticket branch lands its ticket only with that
/// prerequisite, and only when it merged before that landing; any other target counts as before.
/// </summary>
public sealed class StackedMergeLandingTests : IDisposable
{
    private const string Prerequisite = "5";
    private const string PrerequisiteBranch = "agent-smith/5";
    private readonly PrReviewRefreshHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Theory]
    [InlineData("main")]
    [InlineData("agent-smith/99")] // an epic's feature branch: not a ticket this one requires
    public async Task RecordMerged_TargetNotARequiredTicketsBranch_Lands(string? target)
    {
        await EdgeAsync();
        await SeedAsync(Pr1, target, merged: T);

        (await RecordAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task RecordMerged_TargetNotRead_LandsATicketThatRequiresNothing_HoldsOneThatDoes()
    {
        await SeedAsync(Pr1, null, merged: T);
        (await RecordAsync()).Should().BeTrue("with no prerequisite nothing can be stacked");

        await using (var db = _h.Store.Context())
        {
            db.RemoveRange(db.Set<LandedTicket>());
            await db.SaveChangesAsync();
        }
        await EdgeAsync();
        (await RecordAsync()).Should().BeFalse("a host that answered no branch is asked again on the next re-read");
        _h.Landings().Should().BeEmpty();
    }

    [Fact]
    public async Task RecordMerged_IntoAPrerequisitesBranchAfterItsMergeInThatRepo_DoesNotLand_ThoughItsLastRepoMergedLater()
    {
        await EdgeAsync();
        await SeedAsync("https://github.com/o/api/pull/50", "main", merged: T, ticket: Prerequisite);
        await SeedAsync("https://github.com/o/web/pull/51", "main", merged: T.AddHours(5), ticket: Prerequisite, repo: "web");
        await LandPrerequisiteAsync(T.AddHours(5));
        await SeedAsync(Pr1, PrerequisiteBranch, merged: T.AddHours(1));

        (await RecordAsync()).Should().BeFalse("#5's branch in api had merged before this pull request merged into it");
    }

    [Fact]
    public async Task RecordMerged_IntoAPrerequisiteLandedByHandAfterIt_Lands()
    {
        await EdgeAsync();
        await SeedAsync(Pr1, PrerequisiteBranch, merged: T);
        await LandPrerequisiteAsync(T.AddHours(1), TicketLandingKinds.ByHand);

        (await RecordAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task RecordMerged_IntoAnUnlandedPrerequisitesBranch_DoesNotLand()
    {
        await EdgeAsync();
        await SeedAsync(Pr1, PrerequisiteBranch, merged: T);

        (await RecordAsync()).Should().BeFalse("its work sits on #5's branch, not on the default branch");
        _h.Landings().Should().BeEmpty();
    }

    [Fact]
    public async Task RecordMerged_IntoAPrerequisitesBranchThatLandedAfterIt_Lands()
    {
        await EdgeAsync();
        await SeedAsync(Pr1, PrerequisiteBranch, merged: T);
        await LandPrerequisiteAsync(T.AddHours(1));

        (await RecordAsync()).Should().BeTrue("#5's landing carried it onto the default branch");
    }

    [Fact]
    public async Task RecordMerged_IntoAPrerequisitesBranchAfterItLanded_DoesNotLand()
    {
        await EdgeAsync();
        await LandPrerequisiteAsync(T);
        await SeedAsync(Pr1, PrerequisiteBranch, merged: T.AddHours(1));

        (await RecordAsync()).Should().BeFalse("the branch had already landed; this merge never reached the default branch");
    }

    [Fact]
    public async Task RecordMerged_TwoRepositories_OneOnTheDefaultOneOnAnUnlandedPrerequisite_DoesNotLand()
    {
        await EdgeAsync();
        await SeedAsync(Pr1, "main", merged: T);
        await SeedAsync(Pr2, PrerequisiteBranch, merged: T);

        (await RecordAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_MergedPullRequest_ReadsTheBranchItMergedIntoOnce()
    {
        await EdgeAsync();
        await _h.RunAsync("run-1", Pr1);
        _h.State(Pr1, PrReviewStates.Merged);
        _h.Threads([]);
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>())).ReturnsAsync(PrerequisiteBranch);

        await _h.RefreshAsync(Pr1);
        await _h.RefreshAsync(Pr1);

        _h.Records().Single().TargetBranch.Should().Be(PrerequisiteBranch);
        _h.Provider.Verify(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>()), Times.Once);
        _h.Landings().Should().BeEmpty("it merged into #5's branch, which has not landed");
    }

    // 2026-11-24-f9c5c: an open pull request's target is read when first seen and again when its head moved; a merge reads it
    // once more, because a retarget may have come between.
    [Fact]
    public async Task TargetRead_OpenPullRequest_ReadsOncePerHead_AndAgainAtTheMerge()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>())).ReturnsAsync("agent-smith/series/sr-1");
        var read = new PrRecordTargetRead(NullLogger<PrRecordTargetRead>.Instance);
        var first = new PrReviewRecord { Url = Pr1, State = PrReviewStates.Open, HeadSha = "a1" };

        await read.ApplyAsync(provider.Object, first, null, default);
        var stored = new PrReviewRecordView(Pr1, Pr1, "api", "1", "p", "7", "run", PrReviewStates.Open, null, DateTimeOffset.UnixEpoch, null, [])
        {
            HeadSha = "a1", TargetBranch = "agent-smith/series/sr-1",
        };
        var sameHead = new PrReviewRecord { Url = Pr1, State = PrReviewStates.Open, HeadSha = "a1" };
        await read.ApplyAsync(provider.Object, sameHead, stored, default);
        var moved = new PrReviewRecord { Url = Pr1, State = PrReviewStates.Open, HeadSha = "b2" };
        await read.ApplyAsync(provider.Object, moved, stored, default);
        var merged = new PrReviewRecord { Url = Pr1, State = PrReviewStates.Merged, HeadSha = "a1" };
        await read.ApplyAsync(provider.Object, merged, stored, default);

        (first.TargetBranch, sameHead.TargetBranch, merged.TargetBranch).Should().Be(
            ("agent-smith/series/sr-1", "agent-smith/series/sr-1", "agent-smith/series/sr-1"));
        provider.Verify(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>()), Times.Exactly(3),
            "first seen, head moved, merged — the same head reads nothing");
    }

    [Fact]
    public async Task TargetRead_HostRefuses_LeavesTheBranchUnknown()
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("503"));
        var record = new PrReviewRecord { Url = Pr1, State = PrReviewStates.Merged };

        await new PrRecordTargetRead(NullLogger<PrRecordTargetRead>.Instance).ApplyAsync(provider.Object, record, null, default);

        record.TargetBranch.Should().BeNull();
    }

    private Task<bool> RecordAsync() => _h.Recorder().RecordMergedAsync(Project, "7", CancellationToken.None);

    private async Task EdgeAsync()
    {
        await using var db = _h.Store.Context();
        db.Add(new TicketRequirement
        {
            Tracker = "gh-tracker", TicketKey = "github-7", TicketId = "7", Project = "p",
            RequiredTicketKey = $"github-{Prerequisite}", RequiredTicketId = Prerequisite, StartsWhenMet = true, FiledInConversation = "s-1",
        });
        await db.SaveChangesAsync();
    }

    private Task LandPrerequisiteAsync(DateTimeOffset at, string kind = TicketLandingKinds.Merged) =>
        new LandedTicketRepository(_h.Store.ScopeFactory).TryRecordAsync(new TicketLandingFact("gh-tracker", $"github-{Prerequisite}",
            Prerequisite, "p", kind, at, null, null, null), CancellationToken.None);

    private async Task SeedAsync(string url, string? target, DateTimeOffset merged, string ticket = "7", string repo = "api")
    {
        await using var db = _h.Store.Context();
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = repo, Number = url.Split('/')[^1], Project = "p", TicketId = ticket,
            RunId = "run-1", State = PrReviewStates.Merged, StateAtTicks = merged.UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
            TargetBranch = target,
        });
        await db.SaveChangesAsync();
    }
}
