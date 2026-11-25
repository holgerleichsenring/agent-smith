using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Measurement;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Measurement;

/// <summary>2026-11-25-9992k: the comparison numbers over a real migrated store — per run and per series the questions,
/// what was delivered, the cost and per model the calls and tokens, the series' landed count and cost the page's own.</summary>
public sealed class ModelMeasurementReaderTests : IDisposable
{
    private static readonly DateTimeOffset T = TicketStoreHarness.T;
    private readonly TicketStoreHarness _h = new(Register);

    public ModelMeasurementReaderTests() => _h.Config.Projects["alpha"] = new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }],
    };

    [Fact]
    public async Task Run_CarriesQuestionsSpecsDoneCostAndEachModelsShare()
    {
        var run = TicketStoreHarness.Run("run-1", ticket: "1", cost: 0.9m);
        run.RunMetricsJson = RunStoryJson.Serialize(new RunMetrics { QuestionsAsked = 2 });
        await _h.AddAsync(run, Spec("run-1", "a", "done"), Spec("run-1", "b", "failed"),
            Call("run-1", "gpt-6-sol", tokensIn: 100, cached: 900, tokensOut: 300, reasoning: 200, cost: 0.6m),
            Call("run-1", "gpt-6-sol", tokensIn: 50, cached: 0, tokensOut: 10, reasoning: 0, cost: 0.1m),
            Call("run-1", "gpt-5.6", tokensIn: 10, cached: 0, tokensOut: 5, reasoning: 1, cost: 0.2m));

        var view = (await ReadAsync(r => r.RunAsync("run-1", default)))!;

        (view.QuestionsAsked, view.SpecsDelivered, view.CostUsd).Should().Be((2, 1, 0.9m));
        view.Models.Should().Equal(
            new ModelUsageView("gpt-6-sol", 2, 1050, 900, 310, 200, 0.7m),
            new ModelUsageView("gpt-5.6", 1, 10, 0, 5, 1, 0.2m));
    }

    [Fact]
    public async Task Run_Unknown_IsNull() =>
        (await ReadAsync(r => r.RunAsync("none", default))).Should().BeNull();

    [Fact]
    public async Task Series_SumsItsTicketRunsAndItsMaster_PerLandedTicket()
    {
        await _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(
            new SeriesRunRecord("sr-1", "conv-sr-1", "alpha", "operator", true, SeriesRunStates.Running,
                [new SeriesTicket("1", "SCRUM-1", "One", false, T), new SeriesTicket("2", "SCRUM-2", "Two", false, T)], T)
            { MasterQuestionsAsked = 1, MasterCostUsd = 0.5m }, default);
        var one = TicketStoreHarness.Run("run-1", ticket: "1", cost: 1.5m);
        one.RunMetricsJson = RunStoryJson.Serialize(new RunMetrics { QuestionsAsked = 3 });
        await _h.AddAsync(one, TicketStoreHarness.Run("run-2", ticket: "2", cost: 2m),
            TicketStoreHarness.Run("run-9", ticket: "1", project: "beta", cost: 8m), Before(TicketStoreHarness.Run("run-0", ticket: "1", cost: 16m)),
            Call("run-1", "gpt-6-sol", 10, 0, 5, 4, 1.5m), Call("run-2", "gpt-6-sol", 10, 0, 5, 4, 2m),
            new LandedTicket { Tracker = "gh", TicketKey = "github-1", TicketId = "1", Project = "alpha", Kind = "pr", LandedAt = T });

        var view = (await ReadAsync(r => r.SeriesAsync("sr-1", default)))!;

        (view.Tickets, view.TicketsDelivered, view.TicketRuns).Should().Be((2, 1, 2));
        (view.QuestionsAsked, view.MasterQuestionsAsked).Should().Be((4, 1));
        (view.CostUsd, view.MasterCostUsd, view.CostPerDeliveredTicketUsd).Should().Be((4m, 0.5m, 4m));
        view.Models.Should().ContainSingle().Which.Should().Be(new ModelUsageView("gpt-6-sol", 2, 20, 0, 10, 8, 3.5m));
    }

    [Fact]
    public async Task Series_NothingLanded_HasNoCostPerTicket()
    {
        await _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(
            new SeriesRunRecord("sr-1", "conv-sr-1", "alpha", "operator", true, SeriesRunStates.Running,
                [new SeriesTicket("1", "SCRUM-1", "One", false, T)], T), default);

        var view = (await ReadAsync(r => r.SeriesAsync("sr-1", default)))!;

        (view.TicketsDelivered, view.CostPerDeliveredTicketUsd).Should().Be((0, (decimal?)null));
    }

    // A run of the same ticket from before the series is another attempt's.
    private static Run Before(Run run)
    {
        run.StartedAt = T.AddHours(-1);
        run.RunMetricsJson = RunStoryJson.Serialize(new RunMetrics { QuestionsAsked = 9 });
        return run;
    }

    private static RunSpec Spec(string runId, string id, string status) =>
        new() { RunId = runId, SpecId = id, Ordinal = 1, Title = id, Status = status };

    private static RunLlmCall Call(string runId, string model, long tokensIn, long cached, long tokensOut, long reasoning, decimal cost) =>
        new()
        {
            RunId = runId, Model = model, TokensIn = tokensIn, CachedTokensIn = cached, TokensOut = tokensOut,
            ReasoningTokensOut = reasoning, CostUsd = cost,
        };

    private static void Register(IServiceCollection services)
    {
        services.AddSingleton<ISeriesRunStore, SeriesRunRepository>().AddTransient<SeriesRunOrder>();
        services.AddTransient<SeriesRunPullRequests>().AddTransient<SeriesRunTickets>();
        services.AddScoped<SeriesRunProjectReads>().AddScoped<SeriesRunNames>().AddScoped<SeriesRunCosts>()
            .AddScoped<SeriesRunDetailReader>().AddScoped<ModelMeasurementReader>();
        services.AddTransient<SeriesRunHolds>();
    }

    private async Task<TResult> ReadAsync<TResult>(Func<ModelMeasurementReader, Task<TResult>> read)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<ModelMeasurementReader>());
    }

    public void Dispose() => _h.Dispose();
}
