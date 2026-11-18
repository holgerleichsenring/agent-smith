using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>2026-11-17-0207d: the series composition test's boundaries — the clock, the tickets' facts as their runs
/// would leave them, the queued launch the sweep would make, the criteria the ticket branches hold, the tracker.</summary>
internal sealed class SeriesRig(DateTimeOffset start)
{
    public ManualClock Clock { get; } = new(start);
    public ScriptedSeriesFacts Facts { get; } = new(start);
    public List<(string Project, string Pipeline, string Series)> Launches { get; } = [];
    public FilePipelineCompositionTests.FilingTracker Tracker { get; } = new();

    public void Bind(IServiceCollection services)
    {
        services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new FilePipelineCompositionTests.FilingTrackerFactory(Tracker));
        services.RemoveAll<TimeProvider>().AddSingleton<TimeProvider>(Clock);
        services.RemoveAll<ISeriesTicketFacts>().AddSingleton<ISeriesTicketFacts>(Facts);
        services.RemoveAll<ISeriesRunLaunch>().AddSingleton<ISeriesRunLaunch>(new RecordingLaunch(Launches));
        services.RemoveAll<ISeriesCriteria>().AddSingleton<ISeriesCriteria>(new ScriptedCriteria());
    }

    internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    internal sealed class ScriptedSeriesFacts(DateTimeOffset start) : ISeriesTicketFacts
    {
        private readonly Dictionary<string, Func<SeriesTicket, SeriesTicketFact>> _facts = [];
        private static readonly Dictionary<string, string[]> Requires = new() { ["2"] = ["1"], ["3"] = ["1"] };

        public void Set(string id, string status, bool finished = true, bool prOpened = false) =>
            _facts[id] = t => Base(t) with { LatestStatus = status, LatestFinished = finished, OpenedPullRequest = prOpened,
                LatestRunId = $"run-{id}", FirstStartedAt = start, CostUsd = 4m };

        public void Land(string id) => _facts[id] = t => Base(t) with
        {
            LatestStatus = RunStatuses.Success, LatestFinished = true, LandedAt = start.AddHours(3), FirstStartedAt = start, CostUsd = 6m,
        };

        public Task<IReadOnlyList<SeriesTicketFact>> ReadAsync(string project, IReadOnlyList<SeriesTicket> tickets, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SeriesTicketFact>>([.. tickets.Select(t => _facts.TryGetValue(t.TicketId, out var f) ? f(t) : Base(t))]);

        private static SeriesTicketFact Base(SeriesTicket t) => new(t) { Requires = Requires.GetValueOrDefault(t.TicketId) ?? [] };
    }

    private sealed class RecordingLaunch(List<(string, string, string)> launches) : ISeriesRunLaunch
    {
        public Task<string> LaunchAsync(string project, string pipeline, string seriesRunId, CancellationToken ct)
        {
            launches.Add((project, pipeline, seriesRunId));
            return Task.FromResult($"run-{pipeline}-{launches.Count}");
        }

        public Task<bool> FinishedAsync(string runId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class ScriptedCriteria : ISeriesCriteria
    {
        public Task<(IReadOnlyList<SeriesCriterion> Criteria, IReadOnlyList<string> Unread)> ReadAsync(
            SeriesRunRecord record, ResolvedProject project, CancellationToken ct) =>
            Task.FromResult<(IReadOnlyList<SeriesCriterion>, IReadOnlyList<string>)>(
                ([new("#1", "api-spec", "api works"), new("#2", "ui-spec", "ui works")], []));
    }
}
