using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0466: a finished spec is a record you can open. The spec is a ROW the producer
/// wrote, its steps and decisions name it in a column, and the spec it executed is held
/// by the server rather than only by the sandbox that is gone. Proven on a real SQLite
/// engine through the endpoint handlers.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class RunSpecsServedTests : IDisposable
{
    private const string RunId = "2026-08-19T09-00-00-0001";
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-08-19T09:00:00Z");

    private readonly SqliteConnection _connection;
    private readonly IServiceScopeFactory _scopes;

    public RunSpecsServedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var ctx = new AgentSmithDbContext(Options())) MigratedStoreTemplate.CopyInto(ctx);
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunArtifactRepository>();
        _scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunSpecProjection_StateChanged_UpsertsRunSpecsRow()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            Done("p19213a", "Make the thing exist"),
            Selected("p19213b", 2, "Make the thing readable"),
            Failed("p19213b", "Make the thing readable", "dotnet test exited 1"));

        var specs = await ReadSpecsAsync();

        specs.Select(p => p.SpecId).Should().Equal("p19213a", "p19213b");
        specs.Select(p => p.Ordinal).Should().Equal(1, 2);
        specs[0].Status.Should().Be("done");
        specs[0].Title.Should().Be("Make the thing exist");
        specs[0].EndedAt.Should().NotBeNull();
        specs[1].Status.Should().Be("failed");
        specs[1].Verdict.Should().Be("dotnet test exited 1");
    }

    [Fact]
    public async Task RunSpec_SelectedTwice_StaysOneRow()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            Selected("p19213a", 1, "Make the thing exist"));

        (await ReadSpecsAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task RunStep_WrittenDuringASpec_CarriesSpecIdWithoutParsing()
    {
        await ApplyAsync(
            new StepStartedEvent(
                RunId, 4, "p19213a: Implement", 6, T, "p19213a: Implement",
                CommandNames.AgenticMaster, "p19213a"));

        await using var ctx = new AgentSmithDbContext(Options());
        ctx.RunSteps.Single().PhaseId.Should().Be("p19213a");
    }

    [Fact]
    public async Task RunDecision_LoggedDuringASpec_CarriesSpecId()
    {
        await ApplyAsync(new DecisionLoggedEvent(
            RunId, "tooling", "sqlite", "postgres", "smallest footprint", T, "p19213a"));

        await using var ctx = new AgentSmithDbContext(Options());
        ctx.RunDecisions.Single().PhaseId.Should().Be("p19213a");
    }

    /// <summary>
    /// The pre-migration row is what the regex exists for, and nothing backfills it: it
    /// must read exactly as it did before this spec.
    /// </summary>
    [Fact]
    public async Task RunStepsReader_PreMigrationRow_StillSplitsThePrefix()
    {
        await ApplyAsync(new StepStartedEvent(
            RunId, 0, "p19213a: Implement", 1, T, "p19213a: Implement", CommandNames.AgenticMaster));

        var rail = await ReadRailAsync();

        rail.Single().PhaseId.Should().Be("p19213a");
        rail.Single().StepName.Should().Be("Implement");
    }

    /// <summary>
    /// The COLUMN is the spec. A row whose label says one thing and whose column says
    /// another is served the column's answer — otherwise the parser is still deciding.
    /// </summary>
    [Fact]
    public async Task RunStepsReader_RowWithSpecId_DoesNotConsultTheRegex()
    {
        await ApplyAsync(new StepStartedEvent(
            RunId, 0, "p00000z: Implement", 1, T, "p00000z: Implement",
            CommandNames.AgenticMaster, "p19213a"));

        (await ReadRailAsync()).Single().PhaseId.Should().Be("p19213a");
    }

    [Fact]
    public async Task SpecRecord_AfterWrite_IsServedFromTheArtifactStore()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            new PhaseRecordedEvent(RunId, "p19213a", "spec: p19213a\ngoal: \"Make it exist\"\n", T));

        var detail = await ReadSpecAsync("p19213a");

        detail!.Record.Should().Contain("goal: \"Make it exist\"");
        detail.Spec.SpecId.Should().Be("p19213a");
    }

    /// <summary>
    /// 2026-09-17-042eh: the spec review's own artifact row, on the real engine and through
    /// the real applier — upserted under <c>phase_review:&lt;id&gt;</c>, one row per phase,
    /// replaced when a second review of the same spec reports.
    /// </summary>
    [Fact]
    public async Task SpecReviewArtifact_IsUpsertedAsJsonUnderItsOwnKind()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            new PhaseReviewedEvent(
                RunId, "p19213a",
                """{"reviewed":true,"findings":[{"repository":"api","path":"src/A.cs","line":4}]}""", T),
            new PhaseReviewedEvent(RunId, "p19213a", """{"reviewed":true,"findings":[]}""", T.AddMinutes(3)));

        await using var ctx = new AgentSmithDbContext(Options());
        var rows = ctx.Set<AgentSmith.Infrastructure.Persistence.Entities.RunArtifact>()
            .Where(a => a.RunId == RunId && a.Kind.StartsWith("phase_review:"))
            .ToList();

        rows.Should().ContainSingle("a phase has one review row, replaced and never appended")
            .Which.Kind.Should().Be("phase_review:p19213a");
        rows[0].Content.Should().Be("""{"reviewed":true,"findings":[]}""",
            "the second review of a phase replaces the first, as the record artifact does");
    }

    /// <summary>The review row is its own kind, beside the record's — one spec, two artifacts,
    /// neither overwriting the other.</summary>
    [Fact]
    public async Task SpecReviewArtifact_StandsBesideTheRecordArtifact()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            new PhaseRecordedEvent(RunId, "p19213a", "spec: p19213a\n", T),
            new PhaseReviewedEvent(RunId, "p19213a", """{"reviewed":false,"findings":[],"why":"x"}""", T));

        await using var ctx = new AgentSmithDbContext(Options());
        ctx.Set<AgentSmith.Infrastructure.Persistence.Entities.RunArtifact>()
            .Where(a => a.RunId == RunId).Select(a => a.Kind).ToList()
            .Should().BeEquivalentTo(["phase_record:p19213a", "phase_review:p19213a"]);
    }

    /// <summary>2026-10-12-967aa: a record a later release stored under <c>spec_record:</c>, and
    /// the spec-named event names it was replayed from, serve exactly as the spec-named ones.</summary>
    [Fact]
    public async Task SpecRecord_StoredUnderTheSpecKind_IsServed()
    {
        await ApplyAsync(Selected("p19213a", 1, "Make the thing exist"));
        await using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Set<AgentSmith.Infrastructure.Persistence.Entities.RunArtifact>().Add(new()
            {
                RunId = RunId, Kind = RunSpecProjection.AcceptedRecordKindPrefix + "p19213a",
                Content = "spec: p19213a\n",
            });
            await ctx.SaveChangesAsync();
        }

        (await ReadSpecAsync("p19213a"))!.Record.Should().Be("spec: p19213a\n");
    }

    /// <summary>2026-10-12-967aa: a step row whose command carries the spec-block name a later
    /// release writes is classified and labelled as the declared command.</summary>
    [Fact]
    public async Task RunStepsReader_SpecNamedCommand_ClassifiesAsTheDeclaredOne()
    {
        await ApplyAsync(
            new StepStartedEvent(RunId, 0, "Select", 2, T, null, "SelectSpecCommand", "p19213a"),
            new StepStartedEvent(RunId, 1, "Select", 2, T, null, CommandNames.SelectSpec, "p19213a"));

        var rail = await ReadRailAsync();

        rail.Select(s => s.StepClass).Should().Equal(
            [CommandStepClasses.Internal, CommandStepClasses.Internal],
            "an unknown name would read as a milestone");
    }

    [Fact]
    public async Task RunSpecEndpoint_UnknownSpec_IsNotFound() =>
        (await RunSpecQueryEndpoints.GetRunSpecAsync(
            RunId, "p00000a", Specs(), CancellationToken.None))
            .Should().BeOfType<Microsoft.AspNetCore.Http.HttpResults.NotFound>();

    [Fact]
    public async Task RunSpecsEndpoint_ReturnsRows()
    {
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            new StepStartedEvent(
                RunId, 0, "p19213a: Implement", 2, T, "p19213a: Implement",
                CommandNames.AgenticMaster, "p19213a"),
            new DecisionLoggedEvent(
                RunId, "tooling", "sqlite", "postgres", "smallest footprint", T, "p19213a"),
            Done("p19213a", "Make the thing exist"),
            Selected("p19213b", 2, "Make the thing readable"),
            new DecisionLoggedEvent(
                RunId, "ui", "one panel", "two", "fewer places to look", T, "p19213b"),
            // A decision taken outside any spec belongs to none of them.
            new DecisionLoggedEvent(RunId, "run", "retry", null, "transient", T));

        var specs = await ReadSpecsAsync();

        specs.Should().HaveCount(2);
        specs[0].Decisions.Select(d => d.Name).Should().Equal("sqlite");
        specs[0].Steps.Select(s => s.StepName).Should().Equal("Implement");
        specs[1].Decisions.Select(d => d.Name).Should().Equal("one panel");
    }

    [Fact]
    public async Task RunSpecsEndpoint_RunWithoutSpecs_ReturnsEmpty() =>
        (await ReadSpecsAsync()).Should().BeEmpty();

    [Fact]
    public async Task RunSpec_HandedBackOnAFalsePremise_DoesNotReadLikeARedBuild()
    {
        // 2026-09-17-0e79c: the two ask opposite things of the operator — a red build is fixed
        // by working the code, a false premise by amending the specification. A reader telling
        // them apart by the shape of the verdict string would be deriving what the producer
        // already knows, so the distinction is the STATUS itself.
        await ApplyAsync(
            Selected("p19213a", 1, "Make the thing exist"),
            Failed("p19213a", "Make the thing exist", "dotnet test exited 1"),
            Selected("p19213b", 2, "Make the thing readable"),
            HandedBack("p19213b", "Make the thing readable",
                "False premise in p19213b: \"the bus client is a singleton\" — [M1] …"));

        var specs = await ReadSpecsAsync();

        specs[1].Status.Should().Be("handed_back")
            .And.NotBe(specs[0].Status, "a phase never built does not read like one built red");
        new[] { "done", "in_progress", "not_started", "failed" }
            .Should().NotContain(specs[1].Status, "no existing status value may absorb it");
        specs[1].EndedAt.Should().NotBeNull("the phase is over — terminal, not still running");
        specs[1].Verdict.Should().Contain("False premise");
    }

    private static PhaseStateChangedEvent Selected(string specId, int ordinal, string title) =>
        new(RunId, specId, ordinal, title, PhaseRunState.InProgress, null, T);

    private static PhaseStateChangedEvent Done(string specId, string title) =>
        new(RunId, specId, 1, title, PhaseRunState.Done, null, T.AddMinutes(5));

    private static PhaseStateChangedEvent Failed(string specId, string title, string verdict) =>
        new(RunId, specId, 2, title, PhaseRunState.Failed, verdict, T.AddMinutes(9));

    private static PhaseStateChangedEvent HandedBack(string specId, string title, string verdict) =>
        new(RunId, specId, 2, title, PhaseRunState.HandedBack, verdict, T.AddMinutes(9));

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunStepsReader Steps() => new(_scopes, new RunStepAggregatesReader(), new RunRailComposer());
    private RunSpecsReader Specs() => new(_scopes, Steps());

    private async Task ApplyAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        var applier = RunEventAppliers.Default();
        foreach (var ev in events)
        {
            await using var ctx = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(ctx, ev, CancellationToken.None);
        }
    }

    // The endpoint handlers ARE the surface under test — the same code path the
    // dashboard hits, minus the HTTP pipeline.
    private async Task<IReadOnlyList<RunSpecView>> ReadSpecsAsync() =>
        ValueOf<IReadOnlyList<RunSpecView>>(
            await RunSpecQueryEndpoints.GetRunSpecsAsync(RunId, Specs(), CancellationToken.None),
            "specs");

    private async Task<RunSpecDetailView?> ReadSpecAsync(string specId)
    {
        var result = await RunSpecQueryEndpoints.GetRunSpecAsync(
            RunId, specId, Specs(), CancellationToken.None);
        return (result as IValueHttpResult)?.Value as RunSpecDetailView;
    }

    private async Task<IReadOnlyList<RunStepView>> ReadRailAsync() =>
        ValueOf<IReadOnlyList<RunStepView>>(
            await RunStepQueryEndpoints.GetRunStepsAsync(RunId, Steps(), CancellationToken.None),
            "steps");

    private static T ValueOf<T>(IResult result, string property)
    {
        var payload = result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value;
        payload.Should().NotBeNull();
        var value = payload!.GetType().GetProperty(property)!.GetValue(payload);
        return value.Should().BeAssignableTo<T>().Subject;
    }
}
