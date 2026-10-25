using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Memory;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Memory;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Memory.Standing;

/// <summary>
/// 2026-10-25-9324c: at run end each spec's final standing becomes evidence for the memories it relied on —
/// done confirms, failed strains, a hand-back or an already-satisfied spec adds nothing — over the real ledger.
/// </summary>
public sealed class MemoryOutcomeTests : IDisposable
{
    private const string RunId = "2026-10-25T1100-outcome";
    private const string Repo = "github.com/o/api";
    private readonly ServerStateStore _store = new();

    private MemoryEvidenceRepository Ledger => new(_store.ScopeFactory, new());

    private MemoryOutcomeRecorder Recorder => new(Ledger, NullLogger<MemoryOutcomeRecorder>.Instance);

    private static PipelineContext Run(SpecSequenceProgress progress, params string[] reliedSpecs)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, RunId);
        pipeline.Set(ContextKeys.ProjectName, "sample");
        pipeline.Set(ContextKeys.SpecSequenceProgress, progress);
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<MemoryCitation>>>(ContextKeys.MemoryReliance,
            reliedSpecs.ToDictionary(s => s, _ => (IReadOnlyList<MemoryCitation>)[new MemoryCitation("api", Repo, "x")]));
        return pipeline;
    }

    private static SpecSequenceProgress Progress(params (string Id, SpecRunState State)[] specs) =>
        new([.. specs.Select(s => new SpecProgress(s.Id, "goal", s.State))]);

    [Fact]
    public async Task RunEnd_SpecDoneAfterRepair_OneConfirmedNoStrained()
    {
        var progress = Progress(("s-a", SpecRunState.InProgress)).With("s-a", SpecRunState.Failed, "dotnet test")
            .With("s-a", SpecRunState.InProgress).With("s-a", SpecRunState.Done);

        await Recorder.RecordAsync(Run(progress, "s-a"), default);
        await Recorder.RecordAsync(Run(progress, "s-a"), default);

        (await Ledger.OfRepositoryAsync(Repo, default)).Should().ContainSingle().Which.Should()
            .Match<MemoryEvidenceFact>(f => f.Kind == MemoryEvidenceKinds.Confirmed && f.Weight == 1 && f.SpecId == "s-a"
                && f.RunId == RunId && f.Project == "sample" && f.Slug == "x");
    }

    [Fact]
    public async Task RunEnd_SpecFailed_Strained()
    {
        await Recorder.RecordAsync(Run(Progress(("s-a", SpecRunState.Failed), ("s-b", SpecRunState.Done)), "s-a"), default);

        (await Ledger.OfRepositoryAsync(Repo, default)).Should().ContainSingle().Which.Should()
            .Match<MemoryEvidenceFact>(f => f.Kind == MemoryEvidenceKinds.Strained && f.Weight == -0.5 && f.SpecId == "s-a");
    }

    [Fact]
    public async Task RunEnd_HandedBackSpec_NoEvidence()
    {
        var progress = new SpecSequenceProgress([
            new SpecProgress("s-a", "goal", SpecRunState.HandedBack),
            new SpecProgress("s-b", "goal", SpecRunState.Done, Note: SelectSpecHandler.AlreadySatisfiedNote),
            new SpecProgress("s-c", "goal", SpecRunState.InProgress),
        ]);

        await Recorder.RecordAsync(Run(progress, "s-a", "s-b", "s-c"), default);

        (await Ledger.OfRepositoryAsync(Repo, default)).Should().BeEmpty();
    }

    public void Dispose() => _store.Dispose();
}
