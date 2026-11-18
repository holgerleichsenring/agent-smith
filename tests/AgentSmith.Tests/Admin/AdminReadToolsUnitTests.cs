using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Prompts;
using AgentSmith.Server.Services.Admin.Read;
using AgentSmith.Server.Services.Config;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;

namespace AgentSmith.Tests.Admin;

/// <summary>2026-11-17-f3a8b: the admin read tools over a migrated store — pages, filters and bounds.</summary>
public sealed class AdminReadToolsUnitTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly AdminReadSettings _settings = Settings(limit: 2, chars: 100);

    public AdminReadToolsUnitTests() => _db = MigratedStoreTemplate.Context(_connection);

    [Fact]
    public async Task ListRuns_PagesNewestFirstByIdUnderTheLimit()
    {
        foreach (var id in new[] { "r1", "r2", "r3" })
            _db.Runs.Add(new Run { Id = id, Project = "p", Pipeline = "code", Status = "success", StartedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();
        var tools = new AdminRunTools(_db, _settings);

        var first = await tools.ListRuns(limit: 10);
        var next = await tools.ListRuns(beforeId: "r2");

        first.Should().Contain("\"r3\"").And.Contain("\"r2\"").And.NotContain("\"r1\"");
        next.Should().Contain("\"r1\"").And.NotContain("\"r2\"");
        (await tools.ReadRun("nope")).Should().Contain("No run");
    }

    [Fact]
    public async Task ReadRunEvents_FiltersByTypeAndStep()
    {
        _db.RunEvents.AddRange(
            new RunEvent { RunId = "r", Seq = 1, Type = "ToolResult", StepIndex = 1, Timestamp = DateTimeOffset.UtcNow },
            new RunEvent { RunId = "r", Seq = 2, Type = "ToolResult", StepIndex = 2, Timestamp = DateTimeOffset.UtcNow },
            new RunEvent { RunId = "r", Seq = 3, Type = "RunFinished", StepIndex = 2, Timestamp = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        var page = await new AdminEventTools(_db, _settings).ReadRunEvents("r", type: "ToolResult", step: 2);

        page.Should().Contain("\"seq\":2").And.NotContain("\"seq\":1").And.NotContain("RunFinished");
    }

    [Fact]
    public async Task ReadPrompt_AnUnknownPrompt_SaysSo()
    {
        var tools = new AdminPromptTools(new PromptRecordReader(_db), _settings);

        (await tools.ReadPrompt("r", "p-1")).Should().Contain("No prompt 'p-1'");
    }

    [Fact]
    public void Settings_RowsAndChars_AreBoundByTheConfiguredLimits()
    {
        _settings.Rows(null).Should().Be(2);
        _settings.Rows(1).Should().Be(1);
        _settings.Rows(99).Should().Be(2);
        _settings.Chars.Should().Be(100);
    }

    [Fact]
    public async Task ReadSecretsAndHealth_NameSourceAndPresence_NeverAValue()
    {
        var store = new Mock<IConfigStore>();
        store.Setup(s => s.GetSecrets()).Returns([new SecretEntity("gh"), new SecretEntity("jira")]);
        var secrets = new Mock<ISecretValues>();
        secrets.Setup(s => s.Resolve("gh")).Returns("value-of-gh");
        var vault = new Mock<ISecretVault>();
        vault.Setup(v => v.StatesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([
            new AgentSmith.Contracts.Models.StoredSecretState("gh", "ada", DateTimeOffset.UtcNow, "env:1", true, true)]);
        var config = new AdminConfigReadTools(Mock.Of<IConfigurationLoader>(), new ServerContext("x"),
            Microsoft.Extensions.Options.Options.Create(new SandboxGlobalConfig()), null!, null!, secrets.Object,
            new AgentSmith.Server.Services.Admin.Secrets.SecretValuesReader(store.Object, vault.Object));
        var findings = new Mock<IStartupFindings>();
        findings.Setup(f => f.All).Returns([]);

        (await config.ReadSecrets()).Should().Contain("\"present\":true").And.Contain("\"source\":\"stored\"")
            .And.Contain("\"source\":\"missing\"").And.Contain("\"storedBy\":\"ada\"").And.NotContain("value-of-gh");
        new AdminHealthTools(findings.Object).ReadHealth().Should().Contain("replica");
    }

    private static AdminReadSettings Settings(int limit, int chars)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Dialogue = new DialogueGlobalConfig { AdminReadLimit = limit, AdminResultMaxChars = chars },
        });
        return new AdminReadSettings(loader.Object, new ServerContext("x"));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
