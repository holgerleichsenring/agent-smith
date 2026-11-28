using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Specs.MultiRepo;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-db88a: where a run and the host readers take a ticket's spec set from, pinned through the REAL server
/// composition and its database before the source moved. Every arrangement goes through a production writer — the
/// approved record a claim carries, a run's own publish and record, filing's branch write, the pointer the old writer
/// left — so the same assertions hold whether the set is read off the branch or off the TicketSeries row. A stub sandbox
/// does not see another run's pushes: "the next run clones what the last one pushed" is modelled by serving the earlier
/// run's writes as the next run's tracked files.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecSetSourceCompositionTests
{
    private const string Project = "fixture";
    private static readonly string First = HarnessSeries.Base + "a";
    private static readonly string Second = HarnessSeries.Base + "b";

    [Fact]
    public async Task Claim_AFiledTicketWithNothingOnItsBranch_BuildsTheApprovedSetFromTheDatabase()
    {
        await using var harness = Harness();
        ScriptSpec(harness, "Guard.cs");
        ScriptSpec(harness, "Callers.cs");

        var runner = Runner(harness);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        runner.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs
            .Select(p => (p.SpecId, p.State)).Should().Equal((First, SpecRunState.Done), (Second, SpecRunState.Done));
        Writes(harness.StubSandboxFactory!.Spawned).Where(s => s.Path!.Contains("/specs/done/", StringComparison.Ordinal))
            .Select(s => s.Path).Should().SatisfyRespectively(
                a => a.Should().Contain(First), b => b.Should().Contain(Second));
    }

    [Fact]
    public async Task Retry_AfterTheFirstSpecWasRecorded_BuildsOnlyTheNextSpec()
    {
        await using var harness = Harness();
        harness.StubSandboxFactory!.RedAfterWriting("Callers.cs", "dotnet build", "error CS1002: ; expected");
        ScriptSpec(harness, "Guard.cs");
        ScriptSpec(harness, "Callers.cs");
        harness.ChatClient.EnqueueReport(); // b's one repair pass, still red
        var first = Runner(harness);
        await first.RunAsync("code");
        first.LastContext!.Get<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress).Specs
            .Select(p => p.State).Should().Equal(SpecRunState.Done, SpecRunState.Failed);

        var (before, writes) = NextClone(harness);
        harness.StubSandboxFactory.AnswerAfterWriting("Callers.cs", "dotnet build", null); // b's fix is green
        ScriptSpec(harness, "Callers.cs");
        var retry = Runner(harness);
        var result = await retry.RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(writes + 1, "only b is worked again");
        var steps = Writes(harness.StubSandboxFactory.Spawned.Skip(before));
        steps.Should().NotContain(s => s.Path!.Contains($"/specs/done/{First}-", StringComparison.Ordinal), "a is not re-run");
        steps.Should().Contain(s => s.Path!.Contains($"/specs/done/{Second}-", StringComparison.Ordinal));
    }

    // The old writer's branch and row (InFlightSeriesFixture) — the shape every ticket under way at the deploy has.
    [Fact]
    public async Task Claim_ATicketInFlightWithAManifestOnItsBranch_BuildsItsNextUnexecutedSpec()
    {
        await using var harness = Harness();
        harness.StubSandboxFactory!.SourceFiles = InFlightSeriesFixture.Branch();
        await harness.Services.GetRequiredService<ISpecSetPointerStore>().SaveAsync(Project, InFlightSeriesFixture.Pointer(), default);
        ScriptSpec(harness, "Callers.cs");

        var result = await Runner(harness).RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(1, "only b is worked");
        var steps = Writes(harness.StubSandboxFactory.Spawned);
        steps.Should().NotContain(s => s.Path!.Contains($"/specs/done/{First}-", StringComparison.Ordinal), "a ran before the deploy");
        steps.Should().Contain(s => s.Path!.Contains($"/specs/done/{Second}-", StringComparison.Ordinal)
            && s.Content!.Contains("No caller builds its own empty-payload check.", StringComparison.Ordinal));
    }

    // 2026-11-28-db88a: the import happens once. After it, a changed manifest on the branch — b dropped from it, b's file
    // edited — changes nothing: the retry builds b as the stored set states it.
    [Fact]
    public async Task Claim_ATicketInFlightWithAManifestOnItsBranch_ImportsItOnceAndThenReadsOnlyTheDatabase()
    {
        await using var harness = Harness();
        var stub = harness.StubSandboxFactory!;
        stub.SourceFiles = InFlightSeriesFixture.Branch();
        var store = harness.Services.GetRequiredService<ISpecSetPointerStore>();
        await store.SaveAsync(Project, InFlightSeriesFixture.Pointer(), default);
        stub.RedAfterWriting("Callers.cs", "dotnet build", "error CS1002: ; expected");
        ScriptSpec(harness, "Callers.cs");
        harness.ChatClient.EnqueueReport(); // b's repair pass, still red
        await Runner(harness).RunAsync("code");
        var imported = await store.GetSetAsync(Project, "harness-1", default);
        imported!.Specs.Select(s => s.SpecId).Should().Equal(First, Second);
        imported.Executed.Should().Equal(First);

        var (before, writes) = NextClone(harness);
        stub.SourceFiles![InFlightSeriesFixture.ManifestPath] =
            InFlightSeriesFixture.Manifest.Replace($"- {Second}\n", string.Empty, StringComparison.Ordinal);
        stub.SourceFiles[InFlightSeriesFixture.SecondPlanned] = stub.SourceFiles[InFlightSeriesFixture.SecondPlanned]
            .Replace("No caller builds its own empty-payload check.", "Edited on the branch.", StringComparison.Ordinal);
        stub.AnswerAfterWriting("Callers.cs", "dotnet build", null);
        ScriptSpec(harness, "Callers.cs");
        var result = await Runner(harness).RunAsync("code");

        result.IsSuccess.Should().BeTrue(result.Message);
        harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file").Should().Be(writes + 1, "b is built again, a is not");
        Writes(stub.Spawned.Skip(before)).Should().Contain(s => s.Path!.Contains($"/specs/done/{Second}-", StringComparison.Ordinal)
            && s.Content!.Contains("No caller builds its own empty-payload check.", StringComparison.Ordinal)
            && !s.Content.Contains("Edited on the branch.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MultiRepo_ASeriesOverTwoRepositories_ReadsOneSetInIdOrder()
    {
        var branches = new SeriesRepoBranches();
        branches.Repo("Server");
        branches.Repo("Client");
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISandboxFileReaderFactory>();
            services.AddSingleton(branches.Files());
        });
        var pipeline = branches.Pipeline();
        pipeline.Set(ContextKeys.ProjectName, "p");
        RepoConnection server = new() { Name = "Server", Type = RepoType.Local }, client = new() { Name = "Client", Type = RepoType.Local };
        var set = Tests.Specs.MultiRepo.SeriesPerRepoWriteTests.Set(("a", "Server"), ("b", "Client"), ("c", "Server"));
        var published = await harness.Services.GetRequiredService<ISpecSetPublisher>().PublishAsync(
            pipeline, "p", new SeriesScope([server, client], null, [], null), set, [], default);
        published.IsSuccess.Should().BeTrue(published.Message);

        var read = await harness.Services.GetRequiredService<ISpecSetReader>().ReadAsync(
            pipeline, server, [server, client], new TicketKey(SeriesRepoBranches.Key), default);

        read.State.Should().Be(SpecSetBranchState.Answered, read.Why);
        read.Set!.Specs.Select(p => p.SpecId).Should().Equal(
            SeriesRepoBranches.Id('a'), SeriesRepoBranches.Id('b'), SeriesRepoBranches.Id('c'));
        read.Read!.RevisionShas.Should().BeEquivalentTo(new Dictionary<string, string> { ["Server"] = "sha-Server", ["Client"] = "sha-Client" });
        read.Read.Lagging.Should().BeEmpty();
    }

    [Fact]
    public async Task SeriesCheck_ReadsEveryTicketsCriteriaFromTheStoredSet()
    {
        var sources = new RecordingBranchSources();
        await using var harness = HostHarness(sources);
        await FileAsync(harness, "1", "The first ticket's criterion holds.");
        await FileAsync(harness, "2", "The second ticket's criterion holds.");

        var (criteria, unread) = await CheckAsync(harness, integrationBranch: null);

        unread.Should().BeEmpty();
        criteria.Select(c => (c.Ticket, c.Text)).Should().Equal(
            ("#1", "The first ticket's criterion holds."), ("#2", "The second ticket's criterion holds."));
    }

    // 2026-11-27-ecf2d: cleanup deleted the merged ticket branches; the series branch carries what they merged.
    [Fact]
    public async Task SeriesCheck_TicketBranchesDeleted_ReadsTheStoredSet()
    {
        var sources = new RecordingBranchSources();
        await using var harness = HostHarness(sources);
        await FileAsync(harness, "1", "The first ticket's criterion holds.");
        await FileAsync(harness, "2", "The second ticket's criterion holds.");
        const string seriesBranch = "agent-smith/series/sr-1";
        foreach (var write in sources.Writes.ToList()) sources.Seed(write.Repo, seriesBranch, write.Files); // the merges
        foreach (var id in new[] { "1", "2" })
        {
            sources.Gone.Add(TicketBranchNamer.Compose(new TicketId(id)).Value);
            await Landings(harness).TryRecordAsync(new TicketLandingFact(Tracker, $"github-{id}", id, Project,
                TicketLandingKinds.Merged, DateTimeOffset.UtcNow, null, null, $"https://stub.test/pulls/{id}"), default);
        }

        var (criteria, unread) = await CheckAsync(harness, seriesBranch);

        unread.Should().BeEmpty();
        criteria.Select(c => c.Text).Should().Equal("The first ticket's criterion holds.", "The second ticket's criterion holds.");
    }

    private const string Tracker = "github-fixture";

    private static readonly ResolvedProject HostProject = new()
    {
        Name = Project,
        Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.GitHub },
        Repos = [new RepoConnection { Name = "primary", Type = RepoType.Local }],
    };

    private static RealCompositionHarness HostHarness(RecordingBranchSources sources) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
            services.RemoveAll<ISourceProviderFactory>().AddSingleton<ISourceProviderFactory>(sources));

    private static ITicketLandingStore Landings(RealCompositionHarness harness) =>
        harness.Services.CreateScope().ServiceProvider.GetRequiredService<ITicketLandingStore>();

    // Filing's branch write of a one-spec approved set, as the 'file' run makes it.
    private static async Task FileAsync(RealCompositionHarness harness, string id, string done)
    {
        var key = TicketKey.For("github", id).Value;
        var set = HarnessSeries.SetOf(key, [new HarnessSeries.Member($"Goal of ticket {id}", done)], $"2026-11-28-00{id}0");
        var filed = await harness.Services.GetRequiredService<FiledSpecBranch>().WriteAsync(HostProject,
            new SpecApprovalRecord(key, set, ["primary"], Tracker, "primary", id), new TicketId(id),
            new Ticket(new TicketId(id), $"Ticket {id}", "body", null, "open", "github", []), default);
        filed.Written.Should().BeTrue(filed.Error);
    }

    private static async Task<(IReadOnlyList<SeriesCriterion> Criteria, IReadOnlyList<string> Unread)> CheckAsync(
        RealCompositionHarness harness, string? integrationBranch)
    {
        var record = new SeriesRunRecord("sr-1", "conv-1", Project, "sample.user", true, SeriesRunStates.Running,
            [new SeriesTicket("1", "#1", "Ticket 1", false, DateTimeOffset.UtcNow), new SeriesTicket("2", "#2", "Ticket 2", false, DateTimeOffset.UtcNow)],
            DateTimeOffset.UtcNow) { IntegrationBranch = integrationBranch };
        using var scope = harness.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISeriesCriteria>().ReadAsync(record, HostProject, default);
    }

    private static RealCompositionHarness Harness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);

    private static PipelineRunner Runner(RealCompositionHarness harness)
    {
        var runner = new PipelineRunner(harness.Services).WithSeries(HarnessSeries.TwoSpecs);
        runner.RequestContext[ContextKeys.ProjectName] = Project;
        return runner;
    }

    private static void ScriptSpec(RealCompositionHarness harness, string file) =>
        harness.ChatClient
            .EnqueueText($"Planning: {file}.")
            .EnqueueToolCall("write_file", $$"""{"path":"primary/src/{{file}}","content":"// {{file}}"}""")
            .EnqueueReport();

    /// <summary>The next run's clone: every file the runs so far wrote in the primary tree and did not remove, served as
    /// its tracked files; and how many sandboxes and write_file calls there were before it.</summary>
    internal static (int Sandboxes, int WriteCalls) NextClone(RealCompositionHarness harness)
    {
        var stub = harness.StubSandboxFactory!;
        var files = new Dictionary<string, string>(stub.SourceFiles ?? [], StringComparer.Ordinal);
        const string root = "/work/primary/";
        foreach (var step in stub.Spawned.SelectMany(s => s.Sandbox.RanSteps))
        {
            if (step.Kind == StepKind.WriteFile && step.Path is { } path && path.StartsWith(root, StringComparison.Ordinal))
                files[path[root.Length..]] = step.Content ?? string.Empty;
            if (step.Command == "git" && step.Args is ["rm", ..] args)
                foreach (var removed in args.SkipWhile(a => a != "--").Skip(1)) files.Remove(removed);
        }
        stub.SourceFiles = files;
        return (stub.Spawned.Count, harness.ChatClient.ToolCalls.Count(c => c.Name == "write_file"));
    }

    internal static List<Step> Writes(IEnumerable<(SandboxSpec Spec, Tests.TestHelpers.StubSandbox Sandbox)> spawned) =>
        [.. spawned.SelectMany(s => s.Sandbox.RanSteps).Where(s => s.Kind == StepKind.WriteFile && s.Path is not null)];
}
