using System.CommandLine;
using AgentSmith.Cli.Commands;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0515b: the config store refuses to hold two spellings of one name. p0515 made a
/// configured name case-insensitive at RESOLUTION; this is the write side of the same
/// rule. Without it a studio save of 'service.api' finds the stored 'Service.Api',
/// takes the UPDATE branch, and — because the version map holds nothing for the id the
/// caller sent — passes no expected version, so the stale check never fires and the
/// entity that was there is gone.
/// </summary>
public sealed class ConfigNameCollisionTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private const string SampleYaml = """
        agents:
          claude-default: { type: claude, model: sonnet-4 }
        repos:
          test-repo: { type: github, url: https://github.com/test/repo, auth: token }
        trackers:
          test-ado: { type: github, auth: token }
        """;

    private const string CollidingYaml = """
        agents:
          claude-default: { type: claude, model: sonnet-4 }
        repos:
          Service.Api: { type: github, url: https://x, auth: token }
          service.api: { type: github, url: https://y, auth: token }
        trackers:
          test-ado: { type: github, auth: token }
        """;

    [Fact]
    public void Save_ANameDifferingOnlyInCaseFromAStoredOne_IsRefused()
    {
        _h.Store.UpsertRepo(new RepoEntity("Service.Api", "https://x", null), Tester);

        var act = () => _h.Store.UpsertRepo(new RepoEntity("service.api", "https://y", null), Tester);

        act.Should().Throw<ConfigurationException>();
    }

    [Fact]
    public void Save_ANameDifferingOnlyInCase_LeavesTheStoredEntityUnchanged()
    {
        _h.DocStore.Save(Write("Service.Api", "https://x"));

        _h.DocStore.Invoking(s => s.Save(Write("service.api", "https://y")))
            .Should().Throw<ConfigurationException>();

        var rows = _h.DocStore.LoadAll().Where(r => r.Type == ConfigDocTypes.Repo).ToList();
        rows.Should().ContainSingle().Which.Id.Should().Be("Service.Api");
        rows[0].Doc.Should().Contain("https://x");
        rows[0].Version.Should().Be(1, "the refused write never became a version");
    }

    [Fact]
    public void Save_TheSameSpelling_StillUpdatesInPlace()
    {
        _h.DocStore.Save(Write("Service.Api", "https://x"));

        _h.DocStore.Save(Write("Service.Api", "https://y"));

        var row = _h.DocStore.LoadAll().Should().ContainSingle(r => r.Type == ConfigDocTypes.Repo).Subject;
        row.Doc.Should().Contain("https://y");
        row.Version.Should().Be(2);
    }

    [Fact]
    public void Import_ASetCarryingTwoSpellingsOfOneName_IsRefusedBeforeAnythingIsCleared()
    {
        _h.Import(SampleYaml);

        _h.Invoking(h => h.Import(CollidingYaml, force: true))
            .Should().Throw<ConfigurationException>()
            .WithMessage("*'Service.Api'*'service.api'*");

        _h.Store.Load().Repos.Should().ContainSingle(r => r.Id == "test-repo",
            "a refused import must not clear the store it was going to replace");
    }

    [Fact]
    public async Task Import_ThroughTheCli_FailsWithTheSameRefusal()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"agentsmith-p0515b-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var dbPath = Path.Combine(dir, "agentsmith.db");
            var configPath = Path.Combine(dir, "agentsmith.yml");
            await File.WriteAllTextAsync(configPath,
                $"persistence:\n  provider: sqlite\n  connection_string: Data Source={dbPath}\n" + SampleYaml);
            var goodPath = Path.Combine(dir, "good.yml");
            await File.WriteAllTextAsync(goodPath, SampleYaml);
            var collidingPath = Path.Combine(dir, "colliding.yml");
            await File.WriteAllTextAsync(collidingPath, CollidingYaml);
            Migrate(dbPath);

            // The control: the same verb on the same store lands a config without a pair.
            var accepted = await Cli().InvokeAsync(["config", "import", goodPath, "--config", configPath]);
            accepted.Should().Be(0);
            var landed = EntityCount(dbPath);
            landed.Should().BeGreaterThan(0);

            var refused = await Cli().InvokeAsync(
                ["config", "import", collidingPath, "--config", configPath, "--force"]);

            refused.Should().Be(1, "the CLI catches the same ConfigurationException and exits non-zero");
            EntityCount(dbPath).Should().Be(landed,
                "a refused force-import must not clear the store it was going to replace");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Refusal_NamesBothSpellingsAndTheCatalog()
    {
        _h.DocStore.Save(Write("Service.Api", "https://x"));

        var message = _h.DocStore.Invoking(s => s.Save(Write("service.api", "https://y")))
            .Should().Throw<ConfigurationException>().Which.Message;

        // Both spellings are the operator's ONLY handle: a colliding pair is dropped from
        // the catalog whole, so neither half has a row in the studio to click.
        message.Should().Contain("'Service.Api'").And.Contain("'service.api'");
        message.Should().Contain(ConfigDocTypes.Repo, "the message names the catalog the pair is in");
        message.Should().Contain("Export").And.Contain("force", "export, edit, force-import is the way out");
    }

    [Theory]
    [InlineData("Service.Api", "service.api")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("repo-a", "repo-b")]
    [InlineData("ſtrasse", "strasse")]
    public void Refusal_UsesTheSameNameDefinitionAsTheLoader(string first, string second)
    {
        // Comparing one way here and another way in the loader would let the store accept a
        // pair the loader refuses to build. The question is the one the loader asks at
        // lookup time: does the catalog it keys by ConfigNames already answer to this name?
        // U+017F is the discriminating code point — the comparer keeps 'ſ' distinct from
        // 's', so a store that refused that pair would be judging names by a rule of its own.
        var loaderCatalog = ConfigNames.KeyedByName(new Dictionary<string, string> { [first] = "x" });
        var loaderSeesOneName = loaderCatalog.ContainsKey(second);

        _h.DocStore.Save(Write(first, "https://x"));
        var act = () => _h.DocStore.Save(Write(second, "https://y"));

        if (loaderSeesOneName) act.Should().Throw<ConfigurationException>();
        else act.Should().NotThrow();
    }

    private static ConfigDocWrite Write(string id, string url) =>
        new(ConfigDocTypes.Repo, id, $$"""{"type":"github","url":"{{url}}","auth":"token"}""",
            ExpectedVersion: null, [], "tester");

    private static RootCommand Cli() =>
        new() { ConfigCommand.Create(new Option<string>("--config"), new Option<bool>("--verbose")) };

    private static void Migrate(string dbPath)
    {
        using var db = NewContext(dbPath);
        db.Database.Migrate();
    }

    private static int EntityCount(string dbPath)
    {
        using var db = NewContext(dbPath);
        return db.ConfigEntities.Count();
    }

    private static AgentSmithDbContext NewContext(string dbPath) =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={dbPath}").Options);

    public void Dispose() => _h.Dispose();
}
