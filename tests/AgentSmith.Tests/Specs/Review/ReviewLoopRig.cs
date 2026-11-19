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
/// 2026-10-13-8762g: the review's parts as the tests compose them — a scripted reviewer, reviser and evidence check,
/// and a set publisher that takes every revision onto the branch unless told not to. 2026-11-18-9c1cb: the run-time
/// loop and its step are gone; revise_spec and the panel still use these parts.
/// </summary>
internal sealed class ReviewLoopRig
{
    public const string First = "2026-10-14-5678a";
    public const string Second = "2026-10-14-5678b";
    public const string RunId = "run-8762g";

    private readonly Queue<SpecAttackResult?> _answers = new();
    private int _sha;

    public ReviewLoopRig(SpecSet? set = null)
    {
        Pipeline.Set(ContextKeys.SpecSet, set ?? TwoSpecs());
        Pipeline.Set(ContextKeys.ProjectName, "shop-project");
        Pipeline.Set(ContextKeys.SpecRevisionSha, "sha-0");
        Reviewer.Setup(r => r.ReviewAsync(It.IsAny<SpecAttackInput>(), It.IsAny<DerivationLook?>(),
                It.IsAny<AgentConfig>(), It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .Returns((SpecAttackInput input, DerivationLook? _, AgentConfig _, PipelineCostTracker _, CancellationToken _) =>
            {
                lock (Inputs) Inputs.Add(input);
                OnReview?.Invoke(input);
                lock (_answers) return Task.FromResult(_answers.Count > 0 ? _answers.Dequeue() : Build());
            });
        SeriesReviewer.Setup(r => r.ReviewAsync(It.IsAny<SeriesContractInput>(), It.IsAny<DerivationLook?>(),
                It.IsAny<AgentConfig>(), It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .Returns((SeriesContractInput input, DerivationLook? look, AgentConfig _, PipelineCostTracker _, CancellationToken _) =>
            {
                SeriesCalls.Add((input, look?.Terms.Allowance));
                return Task.FromResult(OnSeriesReview(input));
            });
        Evidence.Setup(e => e.CheckAsync(It.IsAny<PipelineContext>(), It.IsAny<SpecDraft>(), It.IsAny<SpecSet>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SpecRoundEvidenceResult([], 0, 1));
        Reviser.Setup(r => r.ReviseAsync(It.IsAny<SpecDraft>(), It.IsAny<IReadOnlyList<SpecReviewCut>>(),
                It.IsAny<IReadOnlyList<SpecEvidenceProblem>>(), It.IsAny<bool>(), It.IsAny<AgentConfig>(),
                It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpecDraft draft, IReadOnlyList<SpecReviewCut> _, IReadOnlyList<SpecEvidenceProblem> _, bool _,
                AgentConfig _, PipelineCostTracker _, CancellationToken _) => new SpecCutRevision(draft, null));
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
    public Mock<ISpecAttackReviewer> Reviewer { get; } = new();
    public Mock<ISeriesContractReviewer> SeriesReviewer { get; } = new();
    public List<(SeriesContractInput Input, int? Allowance)> SeriesCalls { get; } = [];

    /// <summary>2026-10-13-8762k: the series reviewer's answer; BUILD with nothing unless a test says otherwise.</summary>
    public Func<SeriesContractInput, SeriesContractReview> OnSeriesReview { get; set; } =
        _ => new SeriesContractReview(Build(), []);
    public Mock<ISpecRoundEvidence> Evidence { get; } = new();
    public Mock<ISpecCutReviser> Reviser { get; } = new();
    public Mock<Contracts.Specs.ISpecSetPublisher> Publisher { get; } = new();
    public RecordingEventPublisher Events { get; } = new();
    public AsyncLocalRunContextAccessor RunContext { get; } = new();
    public List<SpecAttackInput> Inputs { get; } = [];
    public List<SpecSet> Published { get; } = [];
    public List<int> EventsAtPublish { get; } = [];
    public Action<SpecAttackInput>? OnReview { get; set; }
    public bool WritesToBranch { get; set; } = true;

    public static SpecAttackResult Build() => new(SpecReviewVerdicts.Build, [], []);

    public static SpecAttackResult Cuts(params SpecReviewCut[] cuts) => new(SpecReviewVerdicts.BuildWithCuts, cuts, []);

    public static SpecAttackResult Refuse(string finding = "The cited handler does not exist.") =>
        new(SpecReviewVerdicts.Refuse, [], [new SpecReviewFinding(finding, "shop search:'Handler' [A1]")]);

    /// <summary>The answers the reviewers give, in call order; a call past the list answers BUILD.</summary>
    public ReviewLoopRig Answers(params SpecAttackResult?[] answers)
    {
        foreach (var answer in answers) _answers.Enqueue(answer);
        return this;
    }

    /// <summary>The reviser applies each call's cuts by replacing the given texts, one pair per call.</summary>
    public ReviewLoopRig Revisions(params (string From, string To)[] edits)
    {
        var queue = new Queue<(string From, string To)>(edits);
        Reviser.Setup(r => r.ReviseAsync(It.IsAny<SpecDraft>(), It.IsAny<IReadOnlyList<SpecReviewCut>>(),
                It.IsAny<IReadOnlyList<SpecEvidenceProblem>>(), It.IsAny<bool>(), It.IsAny<AgentConfig>(),
                It.IsAny<PipelineCostTracker>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SpecDraft draft, IReadOnlyList<SpecReviewCut> cuts, IReadOnlyList<SpecEvidenceProblem> _, bool _,
                AgentConfig _, PipelineCostTracker _, CancellationToken _) => cuts.Count == 0 || queue.Count == 0
                ? new SpecCutRevision(draft, null)
                : new SpecCutRevision(CutRevisionFixtures.Draft(Replace(draft.Yaml, queue.Dequeue())), null));
        return this;
    }

    public SpecReviewRun Run(int rounds = 4, int reviewers = 1, int wallSeconds = 1800, string? widerGoal = null) =>
        new(Pipeline, Settings(rounds, reviewers, wallSeconds), new AgentConfig(), widerGoal);

    public static SpecReviewSettings Settings(int rounds = 4, int reviewers = 1, int wallSeconds = 1800, int share = 40) =>
        new(new SpecReviewConfig { Rounds = rounds, Reviewers = reviewers, MaxConcurrent = 2, BudgetSharePercent = share },
            wallSeconds);

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

    private static string Replace(string yaml, (string From, string To) edit) =>
        yaml.Contains(edit.From, StringComparison.Ordinal)
            ? yaml.Replace(edit.From, edit.To, StringComparison.Ordinal)
            : throw new ArgumentException($"the spec has no '{edit.From}'");
}
