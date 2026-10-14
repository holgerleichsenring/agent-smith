using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Passes;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Passes;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Passes;

/// <summary>
/// 2026-10-13-9a59f: pass ends and ledger transitions through the composition root's own registration,
/// over a migrated SQLite store — each written once per trail position, a model call keeping its spec and
/// pass stamps, the passes read over the real rows, and every row dying with its run.
/// </summary>
public sealed class RunPassProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private const string RunId = "2026-10-13T0900-pass";
    private const string Spec = "2026-10-13-1234a";

    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _provider;
    private long _seq;

    public RunPassProjectionTests()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton(Mock.Of<ITicketActivityNudge>());
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton<RunPassesReader>();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RunPass_ReplayedEvent_InsertsOneRow()
    {
        var ended = Ended(1);
        await ApplyAsync(ended, seq: 7);
        await ApplyAsync(ended, seq: 7);

        var row = (await RowsAsync<RunPass>()).Should().ContainSingle().Subject;
        (row.SpecId, row.Ordinal, row.EndedBecause, row.ReengageReason, row.CriteriaMet, row.CriteriaTotal)
            .Should().Be((Spec, 1, MasterPassReasons.Reengaged, MasterPassReasons.RedWithOpenSteps, 1, 2));
        row.CriteriaJson.Should().Contain("\"criterion\":\"it builds\"");
    }

    [Fact]
    public async Task LedgerTransitions_Replayed_InsertOneRow()
    {
        var recorded = Transitions(pass: 2);
        await ApplyAsync(recorded, seq: 9);
        await ApplyAsync(recorded, seq: 9);

        var row = (await RowsAsync<RunLedgerTransition>()).Should().ContainSingle().Subject;
        (row.SpecId, row.MasterPass).Should().Be((Spec, 2));
        row.TransitionsJson.Should().Contain("\"entryId\":\"s1\"");
    }

    [Fact]
    public async Task LlmCall_Stamped_KeepsItsSpecAndPass()
    {
        await ApplyAsync(Call(0.25m, pass: 3));

        var call = (await RowsAsync<RunLlmCall>()).Should().ContainSingle().Subject;
        (call.SpecId, call.MasterPass, call.StepIndex).Should().Be((Spec, 3, 4));
    }

    [Fact]
    public async Task Passes_Read_OverTheStore_PricesEachPassAndAnswersNotFoundForAnUnknownRun()
    {
        await SeedRunAsync();
        await ApplyAsync(Call(0.25m, pass: 1));
        await ApplyAsync(Call(0.5m, pass: 1));
        await ApplyAsync(Transitions(pass: 1));
        await ApplyAsync(Ended(1));

        var view = (await _provider.GetRequiredService<RunPassesReader>().ReadAsync(RunId, CancellationToken.None))!;
        var pass = view.Specs.Should().ContainSingle().Subject.Passes.Should().ContainSingle().Subject;
        (pass.CostUsd, pass.Calls, pass.PassInSpec).Should().Be((0.75m, 2, 1));
        pass.StepsMoved.Select(m => m.EntryId).Should().Equal("s1");

        var missing = await RunPassEndpoints.PassesAsync(
            "no-such-run", _provider.GetRequiredService<RunPassesReader>(), CancellationToken.None);
        missing.Should().BeOfType<NotFound>();
    }

    [Fact]
    public async Task RunDeletion_RemovesPassAndTransitionRows()
    {
        await ApplyAsync(Ended(1));
        await ApplyAsync(Transitions(pass: 1));

        await using (var ctx = new AgentSmithDbContext(Options()))
            await new RunDeletionRepository(ctx).DeleteAsync(RunId, CancellationToken.None);

        (await RowsAsync<RunPass>()).Should().BeEmpty();
        (await RowsAsync<RunLedgerTransition>()).Should().BeEmpty();
    }

    private static MasterPassEndedEvent Ended(int ordinal) =>
        new(RunId, T, Spec, ordinal, 0, MasterPassReasons.First, MasterPassReasons.Reengaged,
            MasterPassReasons.RedWithOpenSteps, VerdictEmitted: true, CriteriaMet: 1, CriteriaTotal: 2,
            [new MasterPassCriterion("it builds", "met", "dotnet build")], "1 of 2 criteria satisfied", 1, 0);

    private static LedgerTransitionsRecordedEvent Transitions(int pass) =>
        new(RunId, """[{"entryId":"s1","activity":"do","from":"pending","to":"done","cause":"model_update","pass":0}]""", T)
        { OriginSpecId = Spec, OriginMasterPass = pass, OriginStepIndex = 4 };

    private static LlmCallFinishedEvent Call(decimal cost, int pass) =>
        new(RunId, "model", "master", 10, 5, cost, 100, T) { OriginSpecId = Spec, OriginMasterPass = pass, OriginStepIndex = 4 };

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private async Task SeedRunAsync()
    {
        await using var ctx = new AgentSmithDbContext(Options());
        ctx.Add(new Run { Id = RunId, Project = "p", Pipeline = "code", Status = "running", StartedAt = T });
        await ctx.SaveChangesAsync();
    }

    private async Task ApplyAsync(AgentSmith.Contracts.Events.RunEvent ev, long? seq = null)
    {
        using var scope = _provider.CreateScope();
        await _provider.GetRequiredService<RunEventApplier>().ApplyAsync(
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ev, seq ?? ++_seq, CancellationToken.None);
    }

    private async Task<List<T>> RowsAsync<T>() where T : class
    {
        await using var ctx = new AgentSmithDbContext(Options());
        return await ctx.Set<T>().Where(r => EF.Property<string>(r, "RunId") == RunId).ToListAsync();
    }
}
