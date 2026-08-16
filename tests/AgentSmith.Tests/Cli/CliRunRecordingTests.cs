using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Cli;

/// <summary>
/// p0423: a CLI run must leave a record. The store it writes to was the reason it did
/// not — the default connection string names a container path a developer machine will
/// not create, and nobody was told the destination did not exist.
/// </summary>
public sealed class CliRunRecordingTests
{
    [Fact]
    public void AnUnwritableStore_FallsBackToTheUserStore_AndSaysSo()
    {
        var config = new PersistenceConfig
        {
            Provider = "sqlite",
            ConnectionString = "Data Source=/var/lib/agentsmith/agentsmith.db",
        };

        var options = CliRunStoreLocation.Resolve(config, out var fellBackTo);

        fellBackTo.Should().NotBeNull("a silent fallback is the same failure in a new place");
        fellBackTo.Should().Contain(".agentsmith");
        options.ConnectionString.Should().Contain(fellBackTo!);
        options.Provider.Should().Be(PersistenceProvider.Sqlite);
    }

    [Fact]
    public void AWritableStore_IsUsedAsConfigured()
    {
        var path = Path.Combine(Path.GetTempPath(), $"as-p0423-{Guid.NewGuid():N}", "runs.db");
        var config = new PersistenceConfig { Provider = "sqlite", ConnectionString = $"Data Source={path}" };

        var options = CliRunStoreLocation.Resolve(config, out var fellBackTo);

        fellBackTo.Should().BeNull();
        options.ConnectionString.Should().Be($"Data Source={path}");
    }

    /// <summary>
    /// A shared provider is never second-guessed: the operator pointed the run at a
    /// server, and quietly writing somewhere else would scatter one run's record across
    /// two stores.
    /// </summary>
    [Fact]
    public void ASharedProvider_IsNeverRedirected()
    {
        var config = new PersistenceConfig
        {
            Provider = "postgresql",
            ConnectionString = "Host=db;Database=agentsmith",
        };

        var options = CliRunStoreLocation.Resolve(config, out var fellBackTo);

        fellBackTo.Should().BeNull();
        options.Provider.Should().Be(PersistenceProvider.Postgresql);
        options.ConnectionString.Should().Be("Host=db;Database=agentsmith");
    }

    /// <summary>
    /// The structural half: registering a store is worth nothing if the graph still
    /// hands every producer the no-op publisher. That one line is why twenty-three live
    /// runs left no record at all.
    /// </summary>
    [Fact]
    public void TheCliGraph_ResolvesAPublisherThatActuallyRecords()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-p0423-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, """
            agents:
              default:
                type: claude
                model: sonnet
            """);
        try
        {
            using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: path, verbose: false, headless: true);

            var publisher = provider.GetRequiredService<AgentSmith.Contracts.Events.IEventPublisher>();

            publisher.Should().NotBeOfType<AgentSmith.Application.Services.Events.NoOpEventPublisher>(
                "a run that writes nothing down makes every question cost another run");
        }
        finally { File.Delete(path); }
    }

    /// <summary>
    /// The end of the story: build the CLI's real graph, publish what a run publishes,
    /// and find it in the store afterwards. This is the criterion the phase exists for —
    /// "the run database is not empty" — and it is checked against the composition root,
    /// not a hand-assembled projector.
    /// </summary>
    [Fact]
    public async Task CliRun_PersistsItsEvents_LikeAServerRun()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"as-p0423-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "runs.db");
        var configPath = Path.Combine(directory, "agentsmith.yml");
        await File.WriteAllTextAsync(configPath, $"""
            agents:
              default:
                type: claude
                model: sonnet
            persistence:
              provider: sqlite
              connection_string: "Data Source={dbPath}"
            """);
        try
        {
            await using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: configPath, verbose: false, headless: true);
            var publisher = provider.GetRequiredService<AgentSmith.Contracts.Events.IEventPublisher>();
            var runId = "2026-08-16T20-00-00-p423";

            await publisher.PublishAsync(new AgentSmith.Contracts.Events.RunStartedEvent(
                runId, "sample", "19106", new[] { "sample-repo" }, DateTimeOffset.UtcNow));
            await publisher.PublishAsync(new AgentSmith.Contracts.Events.RunFinishedEvent(
                runId, "success", null, "done", DateTimeOffset.UtcNow));

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider
                .GetRequiredService<AgentSmith.Infrastructure.Persistence.AgentSmithDbContext>();
            var trail = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.Set<AgentSmith.Infrastructure.Persistence.Entities.RunEvent>());

            trail.Should().NotBeEmpty("a local run must leave a readable record");
            trail.Select(e => e.RunId).Should().OnlyContain(id => id == runId);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>
    /// A traced run leaves one entry per call, in the store — not on a filesystem that
    /// dies with the container that produced it.
    /// </summary>
    [Fact]
    public async Task TracedRun_WritesOneEntryPerCall_BesideTheRunRecord()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"as-p0423t-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var configPath = Path.Combine(directory, "agentsmith.yml");
        await File.WriteAllTextAsync(configPath, $"""
            agents:
              default:
                type: claude
                model: sonnet
            secrets:
              FEED_TOKEN: "glpat-must-not-appear"
            persistence:
              provider: sqlite
              connection_string: "Data Source={Path.Combine(directory, "runs.db")}"
            trace:
              enabled: true
            """);
        try
        {
            await using var provider = AgentSmith.Cli.ServiceProviderFactory.Build(
                configPath: configPath, verbose: false, headless: true);
            var trace = provider.GetRequiredService<AgentSmith.Contracts.Runs.IRunTraceWriter>();
            using var scope = provider.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<AgentSmith.Cli.Services.CliRunRecordingSchema>()
                .EnsureAsync(isLocalSqlite: true, CancellationToken.None);
            var runId = "2026-08-16T21-00-00-p423";

            trace.IsEnabled.Should().BeTrue("the configuration asked for it");
            await trace.WriteAsync(runId, "prompt", "token=glpat-must-not-appear", CancellationToken.None);
            await trace.WriteAsync(runId, "answer", "understood", CancellationToken.None);

            var db = scope.ServiceProvider
                .GetRequiredService<AgentSmith.Infrastructure.Persistence.AgentSmithDbContext>();
            var entries = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                db.Set<AgentSmith.Infrastructure.Persistence.Entities.RunArtifact>());

            entries.Should().HaveCount(2);
            entries.Select(e => e.Kind).Should().BeEquivalentTo(["trace/0001.prompt", "trace/0002.answer"]);
            entries.Should().NotContain(e => e.Content!.Contains("glpat-must-not-appear"),
                "an artefact nobody may share is an artefact nobody will use");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
