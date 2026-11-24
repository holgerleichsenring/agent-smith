using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Pocs.Rollouts;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;

namespace AgentSmith.Tests.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: the Rollout page's PoC facts — the URL only once settled healthy, the GitOps commit linked.</summary>
public sealed class PocRolloutReaderTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;

    public PocRolloutReaderTests() => _db = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public async Task Read_ASettledRun_GivesTheUrlAndTheCommitLink_AFollowingOneNoUrl()
    {
        var poc = PocRows.Poc("todo", "poc-ann");
        poc.State = PocStates.Ready;
        await PocRows.AddAsync(_db, poc);
        _db.Add(new PocRevision
        {
            PocId = poc.Id, Number = 1, UploadSession = "s", UploadId = "u1", State = PocRevisionStates.Built, Actor = "ann-sub", RunId = "run-1",
            CommitSha = "src1", PipelineUrl = "https://host/runs/77", RolloutState = PocRolloutStates.Healthy, RolloutCommitSha = "c0ffee",
            RolloutAtTicks = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero).UtcTicks,
        });
        await _db.SaveChangesAsync();

        var answer = await Reader().ReadAsync(poc.Id, "run-1", default);

        answer!.Url.Should().Be("https://todo.poc.example.internal");
        (answer.Revision, answer.RolloutState, answer.SourceCommitSha, answer.PipelineUrl).Should().Be((1, "healthy", "src1", "https://host/runs/77"));
        answer.GitopsCommitUrl.Should().Be("https://github.com/example/poc-src/commit/c0ffee");
        (await Reader().ReadAsync(poc.Id + 99, "run-1", default)).Should().BeNull();
    }

    private PocRolloutReader Reader()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].SourceRepo = "poc-src";
        var config = new AgentSmithConfig
        {
            Pocs = pocs,
            Repos = new() { ["poc-src"] = new RepoConnection { Name = "poc-src", Type = RepoType.GitHub, Url = "https://github.com/example/poc-src.git" } },
        };
        var projects = new TicketProjects(Mock.Of<IConfigurationLoader>(l => l.LoadConfig(It.IsAny<string>()) == config), new ServerContext("c.yml"));
        return new PocRolloutReader(new PocRepository(_db), new PocRolloutRepository(_db), projects, new PocSourceRepos(Mock.Of<IConnectionRepoUrlBuilder>()));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
