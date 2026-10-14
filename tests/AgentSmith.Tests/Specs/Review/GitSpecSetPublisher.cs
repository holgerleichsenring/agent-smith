using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: a set publisher over a real clone — each spec's yaml written to its planned file and
/// committed, the set and the revision sha put on the run context, as the product's writer does. With
/// <see cref="Fails"/> it puts the set on the context and answers a failure, as a push that was refused does.
/// </summary>
internal sealed class GitSpecSetPublisher(CitationRepo repo, DateTimeOffset at) : ISpecSetPublisher
{
    public bool Fails { get; init; }
    public List<SpecSet> Published { get; } = [];

    public async Task<CommandResult> PublishAsync(
        PipelineContext pipeline, string project, SeriesScope scope, SpecSet set,
        IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken cancellationToken)
    {
        pipeline.Set(ContextKeys.SpecSet, set);
        if (Fails) return CommandResult.Fail($"Spec {set.Key} cannot be written: the push was refused");
        foreach (var spec in set.Specs) await repo.WriteAsync(CitationRepo.SpecPath(spec.SpecId), spec.Draft.Yaml + "\n");
        pipeline.Set(ContextKeys.SpecRevisionSha, await repo.CommitAsync("spec revision", at));
        Published.Add(set);
        return CommandResult.Ok($"Spec {set.Key} published");
    }

    /// <summary>The entry grounding over the real regrounder and this publisher.</summary>
    internal SpecEntryGrounding Grounding() =>
        new(CitationRepo.Regrounder(), new SpecReviewRevision(this), new SpecDraftReader(),
            NullLogger<SpecEntryGrounding>.Instance);

    /// <summary>SelectSpec over this publisher's grounding; no criteria, so no entry account is taken.</summary>
    internal SelectSpecHandler Select() =>
        new(new SpecEntryAccount(TestHelpers.TestGit.Delivery,
                new SpecDeliveryAccounting(TestHelpers.TestGit.Delivery, null!, new SandboxTargets(),
                    NullLogger<SpecDeliveryAccounting>.Instance),
                new SandboxTargets(), NullLogger<SpecEntryAccount>.Instance),
            TestHelpers.TestSpecReview.StartHeads(),
            new SpecProgressRecorder(new Application.Services.Events.NoOpEventPublisher()),
            Grounding(), NullLogger<SelectSpecHandler>.Instance);
}
