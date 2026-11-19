using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: a set publisher that takes every revision onto the branch unless told not to, as the revision tests
/// compose it. 2026-11-19-3e10/4176: no reviewer of any kind is left.
/// </summary>
internal sealed class ReviewLoopRig
{
    public const string First = "2026-10-14-5678a";
    public const string Second = "2026-10-14-5678b";
    public const string RunId = "run-8762g";

    private int _sha;

    public ReviewLoopRig(SpecSet? set = null)
    {
        Pipeline.Set(ContextKeys.SpecSet, set ?? TwoSpecs());
        Pipeline.Set(ContextKeys.ProjectName, "shop-project");
        Pipeline.Set(ContextKeys.SpecRevisionSha, "sha-0");
        Publisher.Setup(p => p.PublishAsync(It.IsAny<PipelineContext>(), It.IsAny<string>(), It.IsAny<SeriesScope>(),
                It.IsAny<SpecSet>(), It.IsAny<IReadOnlyList<IgnoredInstruction>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineContext pipeline, string _, SeriesScope _, SpecSet published,
                IReadOnlyList<IgnoredInstruction> _, CancellationToken _) =>
            {
                Published.Add(published);
                EventsAtPublish.Add(Events.Events.Count);
                pipeline.Set(ContextKeys.SpecSet, published);
                if (WritesToBranch) pipeline.Set(ContextKeys.SpecRevisionSha, $"sha-{++_sha}");
                return CommandResult.Ok("published");
            });
    }

    public PipelineContext Pipeline { get; } = new();
    public Mock<Contracts.Specs.ISpecSetPublisher> Publisher { get; } = new();
    public RecordingEventPublisher Events { get; } = new();
    public AsyncLocalRunContextAccessor RunContext { get; } = new();
    public List<SpecSet> Published { get; } = [];
    public List<int> EventsAtPublish { get; } = [];
    public bool WritesToBranch { get; set; } = true;

    public static SpecAttackResult Build() => new(SpecReviewVerdicts.Build, [], []);

    public static SpecAttackResult Cuts(params SpecReviewCut[] cuts) => new(SpecReviewVerdicts.BuildWithCuts, cuts, []);

    public static SpecAttackResult Refuse(string finding = "The cited handler does not exist.") =>
        new(SpecReviewVerdicts.Refuse, [], [new SpecReviewFinding(finding, "shop search:'Handler' [A1]")]);
    public SpecSet Set => Pipeline.Get<SpecSet>(ContextKeys.SpecSet);

    public static SpecSet TwoSpecs(string? secondRequires = null) => new(
        "shop-42",
        [Spec(CutRevisionFixtures.Draft() with { Repos = ["shop"] }),
         Spec(CutRevisionFixtures.Draft(SecondYaml(secondRequires)) with { Repos = ["shop"] })],
        SpecAccounting.Empty,
        [new SpecRevision(1, "initial derivation", DateTimeOffset.UtcNow)],
        SpecSource.Derived);

    public static DerivedSpec Spec(SpecDraft draft) => new(draft, "export-file", string.Empty, []);

    public static string SecondYaml(string? requires = null) =>
        CutRevisionFixtures.With("requires: [2026-10-14-1234a]", $"requires: [{requires ?? "2026-10-14-1234a"}]",
            CutRevisionFixtures.With("spec: 2026-10-14-5678a", $"spec: {Second}"));
}
