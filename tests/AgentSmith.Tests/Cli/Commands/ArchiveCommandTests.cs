using System.CommandLine;
using AgentSmith.Cli.Commands;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Cli.Commands;

/// <summary>
/// 2026-08-28-2af6: `agentsmith archive export|import` against the database the CONFIG
/// FILE names — no server running, and no connection string on the command line, where it
/// would land in the process list and the shell history.
/// </summary>
public sealed class ArchiveCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("agentsmith-archive-").FullName;

    [Fact]
    public async Task Cli_Export_WritesAnArchiveFromTheConfiguredDatabase()
    {
        var config = await ConfiguredStoreAsync("source", seed: true);
        var archive = Path.Combine(_root, "store.zip");

        var exit = await InvokeAsync("export", archive, config);

        exit.Should().Be(0);
        new FileInfo(archive).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Cli_Import_ReadsAnArchiveIntoAnEmptyDatabase()
    {
        var source = await ConfiguredStoreAsync("source", seed: true);
        var archive = Path.Combine(_root, "store.zip");
        (await InvokeAsync("export", archive, source)).Should().Be(0);
        var target = await ConfiguredStoreAsync("target", seed: false);

        var exit = await InvokeAsync("import", archive, target);

        exit.Should().Be(0);
        await using var db = Context(Path.Combine(_root, "target.db"));
        (await db.RunArtifacts.CountAsync()).Should().Be(4);
        (await db.RunArtifacts.SingleAsync(a => a.Id == FullStoreSeed.VeryLongArtifactId))
            .Content.Should().Be(FullStoreSeed.VeryLongBody);
    }

    [Fact]
    public async Task Cli_Import_AnArchiveThatIsNotThere_FailsWithoutTouchingTheDatabase()
    {
        var config = await ConfiguredStoreAsync("target", seed: false);

        var exit = await InvokeAsync("import", Path.Combine(_root, "absent.zip"), config);

        exit.Should().Be(1);
    }

    [Fact]
    public async Task Cli_Import_ATargetHoldingRows_ReportsTheRefusalAndFails()
    {
        var source = await ConfiguredStoreAsync("source", seed: true);
        var archive = Path.Combine(_root, "store.zip");
        await InvokeAsync("export", archive, source);

        var exit = await InvokeAsync("import", archive, source);

        exit.Should().Be(1, "an import runs into an empty database only");
    }

    private static async Task<int> InvokeAsync(string verb, string archive, string configPath)
    {
        var configOption = new Option<string>("--config", () => configPath, "Path to configuration file");
        var verboseOption = new Option<bool>("--verbose", "Enable verbose logging");
        var root = new RootCommand { ArchiveCommand.Create(configOption, verboseOption) };
        return await root.InvokeAsync(["archive", verb, archive, "--config", configPath]);
    }

    private async Task<string> ConfiguredStoreAsync(string name, bool seed)
    {
        var database = Path.Combine(_root, $"{name}.db");
        MigratedStoreTemplate.CopyToFile(database);
        if (seed)
        {
            await using var db = Context(database);
            await new FullStoreSeed().SeedAsync(db);
        }

        var config = Path.Combine(_root, $"{name}.yml");
        await File.WriteAllTextAsync(config, $"""
            agents:
              default:
                type: claude
                model: sonnet
            persistence:
              provider: sqlite
              connection_string: "Data Source={database}"
            """);
        return config;
    }

    private static AgentSmithDbContext Context(string database) =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite($"Data Source={database}").Options);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
