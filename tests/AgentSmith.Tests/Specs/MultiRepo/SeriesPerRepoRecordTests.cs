using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7d: the record moves an executed spec to done/ in its OWN repository and rewrites that
/// repository's manifest; executed specs are kept by id; and no new spec ever takes an id that is
/// already in a done/ directory or stored as executed.
/// </summary>
public sealed class SeriesPerRepoRecordTests
{
    private const string Done = SeriesPaths.Done;
    private static readonly RepoConnection Worker = new() { Name = "BackgroundWorker" };
    private static readonly RepoConnection Server = new() { Name = "Server" };
    private readonly SeriesRepoBranches _branches = new();

    [Fact]
    public async Task SpecDoneRecorder_SpecForServer_DoneAndManifestInServer()
    {
        _branches.Repo(Worker.Name);
        _branches.Repo(Server.Name);
        var pointers = new InMemorySpecSetPointerStore();
        var set = SeriesPerRepoWriteTests.Set(("a", "Server"), ("b", "Server"));
        var pipeline = RecordPipeline(set);

        var result = await SeriesDoubles.RecordHandler(_branches.Files(), pointers).ExecuteAsync(
            new WritePhaseRecordContext(pipeline.Get<AgentSmith.Domain.Entities.Repository>(ContextKeys.Repository), pipeline,
                [Worker, Server]), default);

        result.IsSuccess.Should().BeTrue(result.Message);
        _branches.Repo(Worker.Name).Paths.Should().BeEmpty(
            "the spec changed nothing in the worker, so its record lies elsewhere");
        (await _branches.Repo(Server.Name).ExistsAsync($"{Done}/{Id('a')}-a.yaml", default)).Should().BeTrue();
        var manifest = new SeriesManifest().Parse(await _branches.Repo(Server.Name)
            .TryReadAsync(SeriesPaths.Manifest(SeriesRepoBranches.Base), default))!;
        manifest.Specs.Should().Equal(Id('a'), Id('b'));
        manifest.Revisions[^1].At.Should().Be(set.Current.At.ToString("O"), "the publish stamp is kept");
        (await _branches.Repo(Server.Name).TryReadAsync("/work/.agentsmith/contexts/default/context.yaml", default))
            .Should().Contain($"-> {Done}/{Id('a')}-a.yaml");
        (await pointers.GetAsync("p", SeriesRepoBranches.Key, default))!.ExecutedIds.Should().Equal(Id('a'));
    }

    [Fact]
    public void DerivedPhaseBuilder_IdInDone_NotReminted()
    {
        const string reply = """
            {"phases": [{"slug": "one", "goal": "Do one", "done": ["One."], "carries": [1]},
                        {"slug": "two", "goal": "Do two", "done": ["Two."], "carries": [1]}]}
            """;
        var segments = TicketSegmenter.Segment("Do one and two.");

        var parsed = DerivationTestParsers.Real().Parse(
            reply, SeriesRepoBranches.Key, SeriesRepoBranches.Base, "4711", segments, SpecSource.Derived,
            taken: [$"{Id('a')}-ran-elsewhere.yaml", Id('c')]);

        parsed.Derivation!.Set.Phases.Select(p => p.PhaseId).Should().Equal([Id('b'), Id('d')],
            "a done/ file and a stored executed id are skipped to the next free letter");
        parsed.Derivation.Set.Phases[1].Draft.Requires.Should().Equal(Id('b'));
    }

    [Fact]
    public void SeriesIdFactory_NextFree_SkipsTakenIdsButNotLongerOnes()
    {
        SeriesIdFactory.NextFree("2026-10-12-0b0b", 0, ["2026-10-12-0b0ba-x.yaml"]).Should().Be(("2026-10-12-0b0bb", 1));
        SeriesIdFactory.NextFree("2026-10-12-0b0b", 1, ["2026-10-12-0b0bab"]).Should().Be(("2026-10-12-0b0bb", 1));
    }

    [Fact]
    public async Task SeriesTakenIds_EveryRepositorysDoneAndTheStoredIds()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T1, null, [Id('a')], executed: Id('a'));
        _branches.Repo(Worker.Name).Seed($"{Done}/{Id('b')}-label.yaml", "spec: x");
        var pointer = new SpecSetPointer(SeriesRepoBranches.Key, "Server", "sha", 1, ExecutedIds: [Id('c')]);

        var taken = await new SeriesTakenIds(_branches.Files(), new Application.Services.Handlers.SandboxTargets())
            .ReadAsync(_branches.Pipeline(), [Worker, Server], pointer, null, default);

        taken.Should().BeEquivalentTo([$"{Id('a')}-label.yaml", $"{Id('b')}-label.yaml", Id('c')]);
    }

    private PipelineContext RecordPipeline(SpecSet set)
    {
        var pipeline = _branches.Pipeline();
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries,
            new Dictionary<string, RemoteContextDiscovery>
            {
                [Worker.Name] = new("default", ".", "C#"), [Server.Name] = new("default", ".", "C#"),
            });
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.ProjectName, "p");
        pipeline.Set(ContextKeys.SpecRepo, Server.Name);
        pipeline.Set(ContextKeys.SpecSet, set);
        pipeline.Set(ContextKeys.PhaseSpec, set.Phases[0].Draft);
        return pipeline;
    }

    private static string Id(char letter) => SeriesRepoBranches.Id(letter);
}
