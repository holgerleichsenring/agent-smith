using AgentSmith.Tests.TestSupport;
using System.CommandLine;
using AgentSmith.Cli.Commands;
using AgentSmith.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Cli.Commands;

/// <summary>
/// 2026-09-30-4bb3: a configuration the import planner refuses — here a role naming a
/// catalog entry the agent does not declare — ends the CLI with the refusal on stderr and
/// exit code 1. The planner used to run outside the only catch, so the refusal escaped as a
/// stack trace; the exit code alone cannot tell the two apart, the stderr text can.
/// </summary>
[Collection("ConsoleOut")]
public sealed class ConfigImportRefusalTests
{
    private const string StoreYaml = """
        agents:
          claude-default: { type: claude, model: claude-haiku-4-5 }
        """;

    private const string UndeclaredUseYaml = """
        agents:
          claude-default:
            type: claude
            catalog:
              haiku: { model: claude-haiku-4-5 }
            models:
              primary: { use: nonesuch }
        """;

    [Fact]
    public async Task Import_PlannerRefusal_PrintsTheRefusalAndExitsOne()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"agentsmith-4bb3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var original = Console.Error;
        try
        {
            var dbPath = Path.Combine(dir, "agentsmith.db");
            var configPath = await WriteAsync(dir, "agentsmith.yml",
                $"persistence:\n  provider: sqlite\n  connection_string: Data Source={dbPath}\n" + StoreYaml);
            var refusedPath = await WriteAsync(dir, "refused.yml", UndeclaredUseYaml);
            CopyMigratedStore(dbPath);
            var stderr = new StringWriter();
            Console.SetError(stderr);

            var exit = await Cli().InvokeAsync(["config", "import", refusedPath, "--config", configPath]);

            exit.Should().Be(1);
            stderr.ToString().Should().Contain("nonesuch", "the operator reads the refusal itself")
                .And.NotContain("   at ", "a refusal is a message, not a stack trace");
            EntityCount(dbPath).Should().Be(0, "nothing is written when the plan is refused");
        }
        finally
        {
            Console.SetError(original);
            Directory.Delete(dir, recursive: true);
        }
    }

    private static async Task<string> WriteAsync(string dir, string name, string content)
    {
        var path = Path.Combine(dir, name);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private static RootCommand Cli() =>
        new() { ConfigCommand.Create(new Option<string>("--config"), new Option<bool>("--verbose")) };

    private static void CopyMigratedStore(string dbPath) => MigratedStoreTemplate.CopyToFile(dbPath);

    private static int EntityCount(string dbPath)
    {
        using var db = NewContext(dbPath);
        return db.ConfigEntities.Count();
    }

    private static AgentSmithDbContext NewContext(string dbPath) =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={dbPath}").Options);
}
