using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-0219: a person's merge marks its refusal on its own merge-green run only, so the series page reads the
/// newest merge run's word per pull request — a refusal or an arm; a merged one, a code run's row and an untouched URL are
/// absent.</summary>
public sealed class PullRequestMergeSaidReaderTests : IDisposable
{
    private const string Pr = "https://dev.azure.com/o/p/_git/api/pullrequest/9";
    private const string Other = "https://dev.azure.com/o/p/_git/api/pullrequest/10";
    private readonly TicketStoreHarness _h = new();

    [Fact]
    public async Task NewestAsync_TheNewestMergeRunsRefusalOrArm_PerUrl()
    {
        var older = TicketStoreHarness.Run("m-1", pipeline: "merge-green");
        var newer = TicketStoreHarness.Run("m-2", pipeline: "merge-green");
        newer.StartedAt = older.StartedAt.AddMinutes(5);
        await _h.AddAsync(older, newer, TicketStoreHarness.Run("code-1"),
            Row("m-1", Pr, PullRequestStatuses.CompletionArmed, "armed"),
            Row("m-2", Pr, PullRequestStatuses.CompletionRefused, "still open — cannot bypass branch policies"),
            Row("code-1", Other, PullRequestStatuses.CompletionRefused, "a code run's row"));

        await using var scope = _h.Services.CreateAsyncScope();
        var said = await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>().NewestAsync([Pr, Other], default);

        said.Keys.Should().Equal(Pr);
        said[Pr].Should().Be(new PullRequestMergeSaid(PullRequestStatuses.CompletionRefused,
            "still open — cannot bypass branch policies", newer.StartedAt));
    }

    [Fact]
    public async Task NewestAsync_ANewerMerge_LeavesNothingToSay()
    {
        var refused = TicketStoreHarness.Run("m-1", pipeline: "merge-green");
        var merged = TicketStoreHarness.Run("m-2", pipeline: "merge-green");
        merged.StartedAt = refused.StartedAt.AddMinutes(5);
        await _h.AddAsync(refused, merged, Row("m-1", Pr, PullRequestStatuses.CompletionRefused, "no"),
            Row("m-2", Pr, PullRequestStatuses.Completed, null));

        await using var scope = _h.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>().NewestAsync([Pr], default)).Should().BeEmpty();
    }

    // 2026-11-24-6811d: the newest refresh-base run per pull request — at work, refused with its reason, failed with its
    // summary — and nothing once a newer one pushed; the merge-green answers are untouched by it.
    [Fact]
    public async Task RefreshesAsync_TheNewestRefreshRun_PerUrl()
    {
        var refused = TicketStoreHarness.Run("r-1", ticket: "", pipeline: "refresh-base");
        var running = TicketStoreHarness.Run("r-2", ticket: "", pipeline: "refresh-base", status: "queued");
        running.StartedAt = refused.StartedAt.AddMinutes(5);
        var failed = TicketStoreHarness.Run("r-3", ticket: "", pipeline: "refresh-base", status: "failed");
        failed.Summary = "the sandbox ran out of memory";
        var pushedBefore = TicketStoreHarness.Run("r-4", ticket: "", pipeline: "refresh-base");
        var pushed = TicketStoreHarness.Run("r-5", ticket: "", pipeline: "refresh-base");
        pushed.StartedAt = pushedBefore.StartedAt.AddMinutes(5);
        const string Third = "https://dev.azure.com/o/p/_git/api/pullrequest/11";
        const string Fourth = "https://dev.azure.com/o/p/_git/api/pullrequest/12";
        await _h.AddAsync(refused, running, failed, pushedBefore, pushed,
            Row("r-1", Pr, PullRequestStatuses.RefreshRefused, "conflicts in Program.cs"),
            new RunRepo { RunId = "r-2", RepoName = "api", PrUrl = Pr },
            new RunRepo { RunId = "r-3", RepoName = "api", PrUrl = Other },
            Row("r-4", Third, PullRequestStatuses.RefreshRefused, "red"),
            new RunRepo { RunId = "r-5", RepoName = "api", PrUrl = Third },
            Row("r-1", Fourth, PullRequestStatuses.RefreshRefused, "conflicts in Startup.cs"));

        await using var scope = _h.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>();
        var said = await reader.RefreshesAsync([Pr, Other, Third, Fourth], default);

        said[Pr].Should().Be(new PullRequestRefreshSaid("r-2", true, null, running.StartedAt));
        said[Other].Should().Be(new PullRequestRefreshSaid("r-3", false, "the sandbox ran out of memory", failed.StartedAt));
        said[Fourth].Reason.Should().Be("conflicts in Startup.cs");
        said.Should().NotContainKey(Third, "the newer refresh pushed");
        (await reader.NewestAsync([Pr], default)).Should().BeEmpty("a refresh is no merge run");
    }

    private static RunRepo Row(string run, string url, string status, string? reason) =>
        new() { RunId = run, RepoName = "api", PrUrl = url, PrStatus = status, Reason = reason };

    public void Dispose() => _h.Dispose();
}
