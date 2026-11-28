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
/// 2026-11-17-ec48a: revise_spec as the tests compose it — the real gate, text, writer and orchestration over
/// <see cref="ReviewLoopRig"/>'s set publisher (2026-11-19-3e10: no reviewer). The run builds the
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
    public PipelineContext Pipeline => Loop.Pipeline;

    public static string RevisedYaml => CutRevisionFixtures.With(Criterion, Revised);

    public MasterSpecRevision Revision() =>
        new(new SpecRevisionGate(Git()), Text(), new SpecRepoJoin(new AgentSmith.Application.Services.Handlers.SandboxTargets()),
            new SpecRevisionWriter(new SpecReviewRevision(Loop.Publisher.Object, new AgentSmith.Application.Services.Persistence.InMemorySpecSetPointerStore()),
            Decisions.Object, Progress.Object, NullLogger<SpecRevisionWriter>.Instance),
            new SpecRetirement(new SpecDraftValidator(new SpecSchemaProvider()), new SpecDraftReader(), new SpecDraftIdRewriter()));

    public Task<string> ReviseAsync(
        string yaml, string reason = "the operator asked for a retry", Func<string, CancellationToken, Task<string>>? ensure = null) =>
        Revision().ReviseAsync(Pipeline, yaml, reason, CancellationToken.None, ensure);

    /// <summary>2026-11-27-3532d: retires the current spec for <paramref name="yaml"/>.</summary>
    public Task<string> RetireAsync(string yaml, string reason = "the spec cannot be implemented as written") =>
        Revision().ReviseAsync(Pipeline, yaml, reason, CancellationToken.None, retire: true);

    public static SpecRevisionText Text() => new(new SpecDraftValidator(new SpecSchemaProvider()), new SpecDraftReader());

    public static SandboxGitOperations Git() => new(AgentSmith.Tests.TestSupport.TestPushers.Plain(), TestSupport.TestGitCredentials.Resolver,
        NullLogger<SandboxGitOperations>.Instance, new TestHelpers.StubSandboxFileReaderFactory(),
        new AgentSmith.Application.Services.Sandbox.SandboxGitIdentity(NullLogger<AgentSmith.Application.Services.Sandbox.SandboxGitIdentity>.Instance));

    public SpecDraft Current => Pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec);
}
