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

    private static RunRepo Row(string run, string url, string status, string? reason) =>
        new() { RunId = run, RepoName = "api", PrUrl = url, PrStatus = status, Reason = reason };

    public void Dispose() => _h.Dispose();
}
