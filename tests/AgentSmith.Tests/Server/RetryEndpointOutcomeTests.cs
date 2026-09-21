using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Lifecycle;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-21-1fa0: the retry endpoint answers the OUTCOME, not the attempt. It used to answer
/// a success body to everything but a missing trigger status, so an operator pressing Retry
/// against a tracker that offered no such move was told it worked.
/// </summary>
public sealed class RetryEndpointOutcomeTests : IDisposable
{
    private const string RunId = "run-1";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public RetryEndpointOutcomeTests()
    {
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
        ctx.Runs.Add(new Run
        {
            Id = RunId, Project = "p1", Pipeline = "fix-bug", TicketId = "42",
            Status = "failed", StartedAt = DateTimeOffset.UtcNow,
        });
        ctx.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RetryEndpoint_AMoveThatLands_AnswersThatTheTicketWasRetried()
    {
        var result = await RunControlEndpoints.RetryAsync(
            RunId, Repository(), Config(), Retry(moved: true), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task RetryEndpoint_AMoveTheTrackerRefuses_AnswersBadRequestNamingTheHold()
    {
        var result = await RunControlEndpoints.RetryAsync(
            RunId, Repository(), Config(), Retry(moved: false), CancellationToken.None);

        var body = result.Should().BeOfType<BadRequest<string>>().Which.Value;
        body.Should().Contain("not retried").And.Contain("keeps the hold");
        body.Should().NotContain("trigger status",
            "that is the OTHER failure, and sending an operator to the project config over a "
            + "tracker that simply offers no such move is the wrong place to look");
    }

    private RunRepository Repository() => new(new AgentSmithDbContext(Options()));

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private static NotImplementableRetryService Retry(bool moved)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.TransitionToAsync(
                It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(moved);
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        return new NotImplementableRetryService(
            new InMemorySpecSetPointerStore(), Mock.Of<IUnmovedTicketStore>(), factory.Object,
            NullLogger<NotImplementableRetryService>.Instance);
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["p1"] = new()
            {
                Name = "p1",
                Tracker = new TrackerConnection { Name = "tracker-a", Type = TrackerType.GitHub },
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "fix-bug", TriggerStatuses = ["Approved"], DoneStatus = "closed",
                },
            },
        },
    };
}
