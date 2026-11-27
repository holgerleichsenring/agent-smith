using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-27-ecf2b: the series page reads which pull requests carry only the spec record (their run's stored
/// outcome) and where each newest attempt's specs sit (its latest cut with specs).</summary>
public sealed class SeriesRunLaneReadsTests : IDisposable
{
    private readonly ServerStateStore _store = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task RecordOnlyAsync_NamesThePullRequestsWhoseRunSaidNoCode()
    {
        await using (var db = _store.Context())
        {
            db.Add(new Run { Id = "r-1", Project = "p", Pipeline = "code", TicketId = "1", Status = RunStatuses.Success, StartedAt = DateTimeOffset.UnixEpoch,
                PullRequestsJson = RunStoryJson.Serialize(new List<RunPullRequestView>
                {
                    new("server", "opened", "https://git/server/pull/1", null, DateTimeOffset.UnixEpoch, true),
                    new("client", "opened", "https://git/client/pull/2", null, DateTimeOffset.UnixEpoch, false),
                    new("worker", "opened", "https://git/worker/pull/3", null, DateTimeOffset.UnixEpoch),
                }) });
            await db.SaveChangesAsync();
        }
        await using var read = _store.Context();

        var recordOnly = await new SeriesRunLaneReads(read).RecordOnlyAsync(["r-1", "r-2"], default);

        recordOnly.Should().BeEquivalentTo([("r-1", "https://git/client/pull/2")]);
    }

    [Fact]
    public async Task SpecReposAsync_TheLatestCutWithSpecs_InSpecOrder()
    {
        await using (var db = _store.Context())
        {
            db.Add(Cut("r-1", ("s-b", 2, "server"), ("s-a", 1, "client")));
            db.Add(Cut("r-1")); // a later hand-back cut carries no specs
            await db.SaveChangesAsync();
        }
        await using var read = _store.Context();

        var repos = await new SeriesRunLaneReads(read).SpecReposAsync(["r-1"], default);

        repos["r-1"].Should().Equal("client", "server");
    }

    private static RunSeriesCut Cut(string runId, params (string Id, int Ordinal, string Repo)[] specs) => new()
    {
        RunId = runId, SetKey = "k", At = DateTimeOffset.UnixEpoch,
        SpecsJson = RunStoryJson.Serialize(specs.Select(s => new SpecCutEntry(s.Id, s.Ordinal, s.Id, "goal", [s.Repo])).ToList()),
    };
}
