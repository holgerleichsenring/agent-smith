using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Application.Services.Specs.Revision;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.Specs.Review;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: revise_spec as the tests compose it — the real gate, text, one-review, writer and orchestration
/// over <see cref="ReviewLoopRig"/>'s scripted reviewer, reviser, evidence check and set publisher. The run builds the
/// rig's first spec; the decision log and the spec's row are recorded.
/// </summary>
internal sealed class SpecRevisionRig
{
    public const string Criterion = "A failed write is logged with the order id.";
    public const string Revised = "A failed write is logged with the order id and retried once.";

    public SpecRevisionRig()
    {
        Loop.Pipeline.Set(ContextKeys.RunId, ReviewLoopRig.RunId);
        Loop.Pipeline.Set(ContextKeys.CurrentSpec, Loop.Set.Specs[0].Draft);
        Decisions.Setup(d => d.LogAsync(It.IsAny<ISandboxFileReader?>(), It.IsAny<DecisionCategory>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Callback((ISandboxFileReader? _, DecisionCategory _, string text, CancellationToken _, string? _) => Logged.Add(text))
            .ReturnsAsync(DecisionLogOutcome.Recorded);
    }

    public ReviewLoopRig Loop { get; } = new();
    public Mock<IDecisionLogger> Decisions { get; } = new();
    public Mock<ISpecProgressRecorder> Progress { get; } = new();
    public List<string> Logged { get; } = [];
    public SpecReviewSettings Settings { get; set; } = ReviewLoopRig.Settings(rounds: 4, reviewers: 1);
    public PipelineContext Pipeline => Loop.Pipeline;

    public static string RevisedYaml => CutRevisionFixtures.With(Criterion, Revised);

    public MasterSpecRevision Revision()
    {
        var reader = new Mock<ISpecReviewSettingsReader>();
        reader.Setup(r => r.Current()).Returns(() => Settings);
        var writer = new SpecRevisionWriter(new SpecReviewRevision(Loop.Publisher.Object),
            new SpecReviewRoundPublisher(Loop.Events, Loop.RunContext, NullLogger<SpecReviewRoundPublisher>.Instance),
            Decisions.Object, Progress.Object, NullLogger<SpecRevisionWriter>.Instance);
        return new MasterSpecRevision(new SpecRevisionGate(Git()), Text(), reader.Object,
            new SpecRevisionReview(new SpecReviewPanel(Loop.Reviewer.Object, DerivationTestLooks.Factory()),
                Loop.Evidence.Object, Loop.Reviser.Object), writer);
    }

    public async Task<string> ReviseAsync(string yaml, string reason = "the operator asked for a retry")
    {
        using var _ = Loop.RunContext.BeginScope(ReviewLoopRig.RunId);
        return await Revision().ReviseAsync(Pipeline, new AgentConfig(), yaml, reason, CancellationToken.None);
    }

    public static SpecRevisionText Text() => new(new SpecDraftValidator(new SpecSchemaProvider()), new SpecDraftReader());

    public static SandboxGitOperations Git() => new(new GitBranchPusher(), TestSupport.TestGitCredentials.Resolver,
        NullLogger<SandboxGitOperations>.Instance, new TestHelpers.StubSandboxFileReaderFactory(),
        new AgentSmith.Application.Services.Sandbox.SandboxGitIdentity(NullLogger<AgentSmith.Application.Services.Sandbox.SandboxGitIdentity>.Instance));

    public SpecDraft Current => Pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec);
}
