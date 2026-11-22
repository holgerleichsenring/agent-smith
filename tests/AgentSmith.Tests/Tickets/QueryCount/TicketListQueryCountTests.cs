using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static AgentSmith.Tests.Tickets.NeverRun.NeverRunFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.QueryCount;

/// <summary>
/// 2026-11-22-9719d: the ticket list's cost does not grow with its tickets — the configuration is loaded a fixed number
/// of times per read, and the never-run rows' start readiness is read set-based, never through the full newest-attempt
/// read per filing, while every dependent still reads ready by the rule a start reads (2026-11-17-b34ec).
/// </summary>
public sealed class TicketListQueryCountTests(ITestOutputHelper output)
{
    private static async Task<TicketStoreHarness> StackedAsync(int dependents, CountingAttempts? counting = null)
    {
        var h = new TicketStoreHarness(counting is null ? null : s => s.AddSingleton<IPreviousAttemptReader>(sp =>
            counting.Bind(new DbPreviousAttemptReader(sp.GetRequiredService<IServiceScopeFactory>(), sp.GetRequiredService<IDecidedDefaults>()))));
        var green = Run("r-58", "58");
        green.PullRequestsJson = """[{"repo":"api","status":"opened","url":"https://git/pr/58"}]""";
        await h.AddAsync(Filing("58", "Orders: API"), green, Step("r-58"));
        for (var i = 0; i < dependents; i++)
        {
            await h.AddAsync(Filing($"{60 + i}", $"Orders: screen {i}"));
            await h.RequiresAsync($"{60 + i}", "58");
        }
        return h;
    }

    private static int ConfigLoads(TicketStoreHarness h) => h.Loader.Invocations.Count(i => i.Method.Name == "LoadConfig");

    [Fact]
    public async Task ListAsync_ConfigurationLoads_DoNotGrowWithTheTickets()
    {
        using var one = await StackedAsync(1);
        using var many = await StackedAsync(12);

        await one.ListAsync();
        await many.ListAsync();

        output.WriteLine($"configuration loads: 1 dependent {ConfigLoads(one)}, 12 dependents {ConfigLoads(many)}");
        ConfigLoads(many).Should().Be(ConfigLoads(one));
        ConfigLoads(many).Should().BeLessThanOrEqualTo(2, "the list's own load and the standings' own load");
    }

    [Fact]
    public async Task ListAsync_ReadinessOfManyFilings_NeverReadsTheFullLatestAttempt()
    {
        var counting = new CountingAttempts();
        using var h = await StackedAsync(12, counting);

        var rows = (await h.ListAsync()).Tickets.Where(t => t.TicketId != "58").ToList();

        rows.Should().HaveCount(12).And.OnlyContain(r => r.Bucket == TicketBucket.Filed && r.WaitsFor!.Count == 0,
            "each dependent stacks on 58's green branch, as a start would");
        counting.Latest.Should().Be(0);
    }

    [Fact]
    public async Task ListAsync_Queries_DoNotGrowWithTheFilings()
    {
        using var few = await StackedAsync(2);
        using var many = await StackedAsync(12);

        few.Commands.Reset();
        await few.ListAsync();
        many.Commands.Reset();
        await many.ListAsync();

        output.WriteLine($"database commands: 2 dependents {few.Commands.Count}, 12 dependents {many.Commands.Count}");
        many.Commands.Count.Should().Be(few.Commands.Count);
    }

    [Fact]
    public async Task ListAsync_ARedPrerequisite_StillHoldsEveryDependent()
    {
        using var h = await StackedAsync(3);
        await h.DeleteRunAsync("r-58");
        await h.AddAsync(Run("r-58b", "58", status: RunStatuses.Failed), Step("r-58b"));

        var rows = (await h.ListAsync()).Tickets.Where(t => t.TicketId != "58").ToList();

        rows.Should().HaveCount(3).And.OnlyContain(r => r.Bucket == TicketBucket.Blocked);
    }

    /// <summary>The attempt reader the list composes, counting the full newest-attempt reads.</summary>
    public sealed class CountingAttempts : IPreviousAttemptReader
    {
        private IPreviousAttemptReader _inner = null!;
        private int _latest;

        public int Latest => _latest;

        public CountingAttempts Bind(IPreviousAttemptReader inner) { _inner = inner; return this; }

        public Task<PreviousAttempt?> LatestAsync(string project, string ticketId, string? excludingRunId, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _latest);
            return _inner.LatestAsync(project, ticketId, excludingRunId, cancellationToken);
        }

        public Task<bool> HasAttemptAsync(string project, string ticketId, CancellationToken cancellationToken) =>
            _inner.HasAttemptAsync(project, ticketId, cancellationToken);

        public Task<IReadOnlySet<string>> AttemptedAsync(string project, IReadOnlyCollection<string> ticketIds, CancellationToken cancellationToken) =>
            _inner.AttemptedAsync(project, ticketIds, cancellationToken);

        public Task<IReadOnlyDictionary<string, PreviousAttempt>> LatestEndsAsync(
            string project, IReadOnlyCollection<string> ticketIds, CancellationToken cancellationToken) =>
            _inner.LatestEndsAsync(project, ticketIds, cancellationToken);

        public Task<bool> HasQueuedAsync(string project, string ticketId, CancellationToken cancellationToken) =>
            _inner.HasQueuedAsync(project, ticketId, cancellationToken);
    }
}
