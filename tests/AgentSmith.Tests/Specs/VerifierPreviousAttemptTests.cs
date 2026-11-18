using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-18-550ab: the live case over two attempts — attempt A failed spec s1 at verification with a gap stored on
/// its RunSpec row; the Retry's attempt B works s1, and its verification is given A's finding and the files B changed,
/// so a gap A missed in code B did not touch is advice, not a second stop.
/// </summary>
public sealed partial class VerifierCarryOverTests : IDisposable
{
    private const string RunA = "2026-11-18T10-26-00-aaaa";
    private const string Gap = "expiry not rejected";
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task TwoAttempts_SecondAttemptsVerification_KeepsItsWordOnTheFirstsFindings()
    {
        await StoreAttemptAAsync(new AcceptanceCriterionView(Criterion, AcceptanceCriterionStatuses.Unmet, Gap));
        var pipeline = AttemptB(RunStatuses.Failed);

        await Findings().RecordAsync(pipeline, "s1", CancellationToken.None);
        var record = AccountRecord.For(pipeline, ["repo: " + File]);

        record.FromPreviousAttempt.Should().BeTrue();
        record.PreviousFindings.Should().Equal($"{Criterion} ({Gap})");
        pipeline.Get<IReadOnlyDictionary<string, string>>(ContextKeys.RepairBaseHeads).Should().ContainKey("repo")
            .WhoseValue.Should().Be("b-start", "the files are named against where attempt B took the spec up");
        var client = new ScriptedChatClient();
        client.EnqueueText($$"""[{"criterion":"{{Criterion}}","disposition":"satisfied","citations":["{{File}}"],"note":"{{Gap}}: CLOSED","advice":"no test for a revoked recipient"}]""");
        var account = await Accountant(client).AccountAsync("repo", [Criterion], Diff, [], new AgentConfig(), null,
            PipelineCostTracker.GetOrCreate(new PipelineContext()), CancellationToken.None, record: record);
        account.Delivered.Should().BeTrue("a gap missed before in code this attempt left alone is advice");
        client.Prompts.Single().Should().Contain("THE PREVIOUS ATTEMPT'S LAST VERIFICATION OF THIS SPEC")
            .And.Contain($"- {Criterion} ({Gap})").And.Contain("FILES THIS ATTEMPT CHANGED SINCE IT TOOK THE SPEC UP")
            .And.NotContain("FILES THE REPAIR CHANGED");
    }

    [Theory]
    [InlineData("shortfall", false, Criterion)]
    [InlineData("success", false, Criterion)]
    [InlineData("cancelled", false, Criterion)]
    [InlineData("failed", true, Criterion)]
    [InlineData("failed", false, "a criterion the spec was rewritten away from")]
    public async Task PreviousAttempt_ShortfallStackedOrRewritten_CarriesNothing(string status, bool stacked, string stored)
    {
        await StoreAttemptAAsync(new AcceptanceCriterionView(stored, AcceptanceCriterionStatuses.Unmet, Gap));
        var pipeline = AttemptB(status);
        if (stacked) pipeline.Set(ContextKeys.StackedOnTicket, "6");

        await Findings().RecordAsync(pipeline, "s1", CancellationToken.None);

        pipeline.Has(ContextKeys.PreviousAttemptFindings).Should().BeFalse();
        pipeline.Has(ContextKeys.RepairBaseHeads).Should().BeFalse("no list, no changed files to name");
    }

    [Fact]
    public async Task PreviousAttempt_StoredNoAccountForTheSpec_CarriesNothing()
    {
        var pipeline = AttemptB(RunStatuses.Failed);

        await Findings().RecordAsync(pipeline, "s1", CancellationToken.None);

        pipeline.Has(ContextKeys.PreviousAttemptFindings).Should().BeFalse();
    }

    [Fact]
    public void Record_ARepairListWinsOverThePreviousAttempt()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PreviousAttemptFindings, new List<string> { "c (from attempt A)" });
        pipeline.Set(ContextKeys.OutstandingCriteria, new List<string> { "repo: c (from this attempt)" });

        var record = AccountRecord.For(pipeline, []);

        record.PreviousFindings.Should().Equal("repo: c (from this attempt)");
        record.FromPreviousAttempt.Should().BeFalse();
    }

    [Fact]
    public void RelationalComposition_ReadsTheStoredAcceptanceFromTheStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AgentSmith.Contracts.Services.IRunSpecAcceptanceReader, AgentSmith.Application.Services.Resume.NullRunSpecAcceptanceReader>();
        AgentSmith.Server.Extensions.RelationalPersistenceExtensions.AddRelationalPersistence(services);

        services.Last(d => d.ServiceType == typeof(AgentSmith.Contracts.Services.IRunSpecAcceptanceReader))
            .ImplementationType.Should().Be(typeof(DbRunSpecAcceptanceReader));
    }

    private PreviousAttemptFindings Findings()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Db());
        return new PreviousAttemptFindings(
            new DbRunSpecAcceptanceReader(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>()),
            NullLogger<PreviousAttemptFindings>.Instance);
    }

    private async Task StoreAttemptAAsync(AcceptanceCriterionView row)
    {
        await using var db = Db();
        var json = RunStoryJson.Serialize(new AcceptanceView([row], "verbatim", "the ratified spec", AcceptanceSources.DeliveryAccount));
        await new RunSpecProjection().ApplyStateAsync(db, new SpecStateChangedEvent(
            RunA, "s1", 1, "goal", SpecRunState.Failed, "VerifySpec", DateTimeOffset.UnixEpoch, json), CancellationToken.None);
    }

    private static PipelineContext AttemptB(string previousStatus)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("7"));
        pipeline.Set(ContextKeys.CurrentSpec, new SpecDraft("s1", "goal", "spec: s1", []) { Done = [Criterion] });
        pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt(RunA, previousStatus, DateTimeOffset.UnixEpoch, true));
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SpecStartHeads, new Dictionary<string, string> { ["repo"] = "b-start" });
        return pipeline;
    }

    private AgentSmithDbContext Db() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
