using AgentSmith.Application.Services.Holds;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992a: one truth — the master's facts (<see cref="ISeriesTicketFacts"/>) and the series page read
/// one state through the real composition over a migrated store: the same CI, the same kind, the same words.</summary>
public sealed class SeriesStatusReaderTests : IDisposable
{
    private static readonly DateTimeOffset T = TicketStoreHarness.T;
    private readonly TicketStoreHarness _h = new(services =>
    {
        services.AddSingleton<ISeriesRunStore, SeriesRunRepository>().AddTransient<SeriesRunOrder>();
        services.AddTransient<SeriesRunPullRequests>().AddTransient<SeriesRunTickets>().AddTransient<SeriesRunHolds>();
        services.AddScoped<SeriesRunProjectReads>().AddScoped<SeriesRunNames>().AddScoped<SeriesRunCosts>().AddScoped<SeriesRunDetailReader>()
            .AddScoped<SeriesParkedQuestion>().AddScoped<SeriesStatusReader>()
            .AddScoped<ISeriesTicketFacts>(sp => sp.GetRequiredService<SeriesStatusReader>());
    });

    public SeriesStatusReaderTests() => _h.Config.Projects["alpha"] = new()
    {
        Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }, new RepoConnection { Name = "web", DefaultBranch = "main" }],
    };

    [Fact]
    public async Task AClosedPullRequestsRedHead_IsNotTheTicketsCi_ForTheMasterNorThePage()
    {
        var closed = Record("1", "api", CiStates.Red, number: "7");
        closed.State = PrReviewStates.Closed;
        await _h.AddAsync(closed, Record("1", "web", CiStates.Green), TicketStoreHarness.Run("run-1", ticket: "1", cost: 2m));

        var (fact, page) = await ReadAsync();

        (fact.Ci, page.Ci, fact.CiFailure).Should().Be((CiStates.Green, CiStates.Green, null));
        (page.Kind, page.State).Should().Be((fact.Kind, fact.State));
        fact.CostUsd.Should().Be(2m);
    }

    [Fact]
    public async Task ACiReadOnAnOlderHead_IsCiRunningSinceTheHeadMoved_OnTheRowInTheHoldAndForTheMaster()
    {
        var record = Record("1", "api", CiStates.Green);
        (record.CiHeadSha, record.HeadMovedAtTicks) = ("old", T.AddHours(1).UtcTicks);
        await _h.AddAsync(record, TicketStoreHarness.Run("run-1", ticket: "1"));

        var (fact, page) = await ReadAsync();

        page.PullRequests.Single().Ci.Should().Be(CiStates.Pending, "the hold's rule: the new head's CI is not read yet");
        (page.Hold!.Case, page.Hold.Since).Should().Be((TicketHoldCases.CiRunning, T.AddHours(1)));
        fact.State.Should().Be(page.State).And.StartWith("CI running since ");
        (fact.Kind, page.Kind, fact.Ci).Should().Be((SeriesTicketKinds.Pr, SeriesTicketKinds.Pr, CiStates.Pending));
    }

    [Fact]
    public async Task ATicketARunWorksOn_IsWhatTheSeriesWorksOn()
    {
        await _h.AddAsync(TicketStoreHarness.Run("run-1", ticket: "1", status: "running"));

        var (fact, page) = await ReadAsync();
        var detail = (await Detail())!;

        (fact.Working, page.Working, page.Kind).Should().Be((true, true, SeriesTicketKinds.Running));
        detail.WorkingOn.Should().Equal("#1");
    }

    private async Task<(SeriesTicketFact Fact, AgentSmith.Server.Models.SeriesRunTicketView Page)> ReadAsync()
    {
        var record = new SeriesRunRecord("sr-1", "conv-1", "alpha", "operator", true, SeriesRunStates.Running,
            [new SeriesTicket("1", "#1", "work", false, T)], T);
        await _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(record, default);
        await using var scope = _h.Services.CreateAsyncScope();
        var facts = await scope.ServiceProvider.GetRequiredService<ISeriesTicketFacts>().ReadAsync(record, default);
        return (facts.Single(), (await Detail())!.Tickets.Single());
    }

    private async Task<AgentSmith.Server.Models.SeriesRunDetailView?> Detail()
    {
        await using var scope = _h.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SeriesRunDetailReader>().ReadAsync("sr-1", default);
    }

    private static PrReviewRecord Record(string ticket, string repo, string ci, string? number = null)
    {
        var url = $"https://github.com/o/{repo}/pull/{number ?? ticket}";
        return new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = repo, RepoKey = RepoKey.Of($"https://github.com/o/{repo}"), Number = number ?? ticket,
            Project = "alpha", TicketId = ticket, RunId = $"run-{ticket}", State = PrReviewStates.Open, HeadSha = "abc",
            CheckedAtTicks = T.UtcTicks, CiState = ci, CiHeadSha = "abc", CiFailuresJson = PrRecordCiJson.Failures([]),
        };
    }

    public void Dispose() => _h.Dispose();
}
