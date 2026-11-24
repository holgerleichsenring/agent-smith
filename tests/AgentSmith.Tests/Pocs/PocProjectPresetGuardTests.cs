using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Services.Pocs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: a server-provisioned poc- project (a Poc row names it) runs only the PoC presets; an operator's own
/// poc-* project (no Poc row) runs everything and is named by a finding while PoCs are on.
/// </summary>
public sealed class PocProjectPresetGuardTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _services;

    public PocProjectPresetGuardTests()
    {
        _services = new ServiceCollection()
            .AddDbContext<AgentSmithDbContext>(b => b.UseSqlite(_connection), ServiceLifetime.Scoped)
            .AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>())
            .BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Guard_CodeInAProvisionedPocProject_IsRefusedNamingThePreset()
    {
        await SeedAsync();

        var act = () => Guard().CheckAsync("poc-ann", PipelinePresets.CodeName, CancellationToken.None);

        (await act.Should().ThrowAsync<ConfigurationException>()).Which.Message
            .Should().Contain("'code'").And.Contain("poc-ann").And.Contain("poc- projects run only the PoC presets");
    }

    [Fact]
    public async Task Guard_PocDeployInAProvisionedPocProject_Passes()
    {
        await SeedAsync();

        await Guard().Invoking(g => g.CheckAsync("poc-ann", PipelinePresets.PocDeployName, CancellationToken.None)).Should().NotThrowAsync();
    }

    [Fact]
    public async Task Guard_AnOperatorsPocNamedProject_RunsCode()
    {
        await SeedAsync();

        await Guard().Invoking(g => g.CheckAsync("poc-demo", PipelinePresets.CodeName, CancellationToken.None)).Should().NotThrowAsync();
        (await new DbPocProjects(Scopes()).IsAsync("poc-demo", CancellationToken.None)).Should().BeFalse();
        (await new DbPocProjects(Scopes()).IsAsync("shop", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task NoDatabase_NoProjectIsAPocProject()
    {
        (await new NoPocProjects().IsAsync("poc-ann", CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task PrefixFindings_NameTheOperatorsPocNamedProject_OnlyWhilePocsAreOn(bool enabled, int named)
    {
        await SeedAsync();
        var findings = new Mock<IStartupFindings>();
        var config = new AgentSmithConfig
        {
            Pocs = new PocsConfig { Enabled = enabled },
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["poc-ann"] = new() { Name = "poc-ann" }, ["poc-demo"] = new() { Name = "poc-demo" }, ["shop"] = new() { Name = "shop" },
            },
        };

        var count = await new PocPrefixFindings(new DbPocProjects(Scopes()), findings.Object).ReportAsync(config, CancellationToken.None);

        count.Should().Be(named);
        findings.Verify(f => f.Clear("pocs"), Times.Once);
        findings.Verify(f => f.Record(It.Is<StartupFinding>(x => x.Project == "poc-demo" && x.Field == "pocs")), Times.Exactly(named));
    }

    private PocProjectPresetGuard Guard() => new(new DbPocProjects(Scopes()));

    private IServiceScopeFactory Scopes() => _services.GetRequiredService<IServiceScopeFactory>();

    private async Task SeedAsync()
    {
        using var scope = _services.CreateScope();
        await PocRows.AddAsync(scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>(), PocRows.Poc("todo", "poc-ann"));
    }
}
