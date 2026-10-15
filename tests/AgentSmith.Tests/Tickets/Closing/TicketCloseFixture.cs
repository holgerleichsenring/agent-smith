using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Closing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: the close act over the store harness's GitHub project "alpha" (tracker "gh") with two
/// repositories — "api" on a host that closes pull requests, "local" on one that cannot.
/// </summary>
internal sealed class TicketCloseFixture : IDisposable
{
    public const string Tracker = "gh";

    public TicketCloseFixture()
    {
        H.Config.Projects["alpha"] = new ResolvedProject
        {
            Name = "alpha", Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
            Repos = [new RepoConnection { Name = "api" }, new RepoConnection { Name = "local" }],
        };
        var host = new Mock<ISourceProvider>();
        Closer = host.As<IPullRequestCloser>();
        H.Sources.Setup(s => s.Create(It.Is<RepoConnection>(r => r.Name == "api"))).Returns(host.Object);
        H.Sources.Setup(s => s.Create(It.Is<RepoConnection>(r => r.Name == "local"))).Returns(Mock.Of<ISourceProvider>());
        H.Tracker.Setup(t => t.CloseTicketAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    public TicketStoreHarness H { get; } = new();
    public Mock<IPullRequestCloser> Closer { get; }
    public TicketCloseAct Act => H.Services.GetRequiredService<TicketCloseAct>();
    public ITicketClosedMarkStore Marks => H.Services.GetRequiredService<ITicketClosedMarkStore>();

    public static string Key(string id) => $"github-{id}";

    public static string Url(string number, string repo = "api") => $"https://github.com/o/{repo}/pull/{number}";

    /// <summary>A code run of ticket 42 naming the pull requests, started at T.</summary>
    public static Run Named(string id, string status, params (string Repo, string Number)[] prs)
    {
        var run = TicketStoreHarness.Run(id, "42", status);
        run.PullRequestsJson = RunStoryJson.Serialize(prs
            .Select(p => new RunPullRequestView(p.Repo, PullRequestStatuses.Opened, Url(p.Number, p.Repo), null, T)).ToList());
        return run;
    }

    public static PrReviewRecord Record(string number, string state, DateTimeOffset? stateAt = null, string repo = "api") => new()
    {
        PrKey = PrKey.Of(Url(number, repo)), Url = Url(number, repo), Repo = repo, Number = number, Project = "alpha", TicketId = "42",
        RunId = "r-1", State = state, StateAtTicks = (stateAt ?? T).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
    };

    public Task<TicketCloseResult> CloseAsync(
        string? note = null, bool prs = false, bool tracker = false, string ticket = "42", string project = "alpha", CancellationToken ct = default) =>
        Act.CloseAsync(project, ticket, new TicketCloseRequest(note, prs, tracker), "Ada", ct);

    public async Task<TicketClosedMark?> MarkAsync(string ticket = "42") =>
        (await Marks.OfAsync(Tracker, [Key(ticket)], default)).SingleOrDefault();

    public void Dispose() => H.Dispose();
}
