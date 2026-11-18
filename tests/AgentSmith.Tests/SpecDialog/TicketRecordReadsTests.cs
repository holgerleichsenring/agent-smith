using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Admin.Read;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-18-d6d2b: the steward borrows the admin assistant's event and configuration reads, scoped to its ticket —
/// its own runs and a sibling project's run on the same tracker read; another ticket's run is refused by name.
/// </summary>
public sealed class TicketRecordReadsTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _services;

    public TicketRecordReadsTests()
    {
        _services = Services(_connection);
        using var db = MigratedStoreTemplate.Context(_connection);
        db.Runs.AddRange(Run("r-own", "p", "42"), Run("r-sibling", "sib", "42"), Run("r-other", "p", "7"),
            Run("r-foreign", "elsewhere", "42"));
        foreach (var id in new[] { "r-own", "r-sibling", "r-other", "r-foreign" })
            db.RunEvents.Add(new RunEvent { RunId = id, Seq = 1, Type = "SandboxResult", Timestamp = DateTimeOffset.UtcNow, PayloadJson = "{\"OutputTail\":\"marker\"}" });
        db.SaveChanges();
    }

    [Fact]
    public void Tools_AreTheEventAndConfigReads_WithoutModelCallsPromptsOrSecrets() =>
        Sut().Tools().Select(t => t.Name).Should().BeEquivalentTo(["read_run_events", "read_config"]);

    [Theory]
    [InlineData("r-own")]
    [InlineData("r-sibling")]
    public async Task ReadRunEvents_TheTicketsRun_InThisOrASiblingProject_Reads(string runId) =>
        (await InvokeAsync(runId)).Should().Contain("marker");

    [Theory]
    [InlineData("r-other")]
    [InlineData("r-foreign")]
    public async Task ReadRunEvents_AnotherTicketsRun_IsRefusedByName(string runId) =>
        (await InvokeAsync(runId)).Should().Contain($"Run '{runId}' is not one of ticket 42's runs").And.NotContain("marker");

    private async Task<string> InvokeAsync(string runId)
    {
        var tool = (AIFunction)Sut().Tools().Single(t => t.Name == "read_run_events");
        return (await tool.InvokeAsync(new AIFunctionArguments { ["runId"] = runId }))?.ToString() ?? string.Empty;
    }

    private TicketRecordReads Sut() => new(
        _services.GetRequiredService<IServiceScopeFactory>(), new SecretMasker(Mock.Of<ISecretValues>(s => s.All() == Array.Empty<string>())), "p", "42");

    private static Run Run(string id, string project, string ticket) =>
        new() { Id = id, Project = project, TicketId = ticket, Pipeline = "code", Status = "failed", StartedAt = DateTimeOffset.UtcNow };

    private static ServiceProvider Services(SqliteConnection connection)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Dialogue = new DialogueGlobalConfig { AdminReadLimit = 50, AdminResultMaxChars = 10_000 },
            Projects = new()
            {
                ["p"] = Project("p", "boards"), ["sib"] = Project("sib", "boards"), ["elsewhere"] = Project("elsewhere", "other"),
            },
        });
        var services = new ServiceCollection();
        services.AddScoped(_ => new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>());
        services.AddSingleton(loader.Object).AddSingleton(new ServerContext("x"));
        services.AddScoped<AdminReadSettings>().AddScoped<AdminEventTools>();
        services.AddTransient<TicketProjects>().AddTransient<FiledWorkTrackerProjects>().AddTransient<TicketRunsQuery>();
        return services.BuildServiceProvider();
    }

    private static ResolvedProject Project(string name, string tracker) =>
        new() { Name = name, Tracker = new TrackerConnection { Name = tracker, Type = TrackerType.AzureDevOps } };

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
