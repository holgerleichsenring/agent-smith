using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-09-17-0e79a: one DeriveSpec graph over a seeded approval store and a seeded ticket
/// branch, so the approved-set cases run the real reader, the real source precedence and the
/// real publisher rather than a hand-assembled copy of them.
/// </summary>
internal sealed class ApprovedSetHarness
{
    internal const string Repo = "primary";

    internal InMemorySpecApprovalStore Approvals { get; } = new();

    internal InMemorySpecSetPointerStore Pointers { get; } = new();

    internal SpecBranchFiles Branch { get; init; } = new();

    internal RecordingSpecSetWriter Writer { get; } = new();

    internal RecordingPullRequestOpener PullRequests { get; } = new();

    internal CountingDeriver Deriver { get; } = new();

    internal string? PointerSha { get; set; }

    internal DeriveSpecHandler Handler()
    {
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var draftReader = new PhaseDraftReader();
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(Branch);
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), NullLogger<SandboxGitOperations>.Instance, factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        if (PointerSha is { } sha)
            Pointers.SaveAsync(string.Empty, new SpecSetPointer(Branch.Key, Repo, sha, 1), default)
                .GetAwaiter().GetResult();
        var tickets = new Mock<ITicketProviderFactory>();
        return new DeriveSpecHandler(
            Deriver,
            new SpecSetReader(
                factory.Object, gitOps, draftReader, new SpecSetIndex(), new SandboxTargets(),
                NullLogger<SpecSetReader>.Instance),
            Publisher(),
            Pointers,
            new ApprovedSpecSetResolver(Approvals, NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new PhaseSpecFromTicket(validator, draftReader),
                new ApprovedSetSource(NullLogger<ApprovedSetSource>.Instance),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                NullLogger<SpecSourceResolver>.Instance),
            new SpecFallback(validator, draftReader, new DerivedPhaseYamlRenderer()),
            new SpecCoverageRefusal(
                new SpecCutGate(new NoOpEventPublisher(), NullLogger<SpecCutGate>.Instance),
                new SpecFallback(validator, draftReader, new DerivedPhaseYamlRenderer())),
            new SpecSetTicketCommenter(tickets.Object, NullLogger<SpecSetTicketCommenter>.Instance),
            new SpecCutGate(new NoOpEventPublisher(), NullLogger<SpecCutGate>.Instance),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(tickets.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            NullLogger<DeriveSpecHandler>.Instance);
    }

    /// <summary>The real publisher: the writer, the pointer recorder and the draft pull request.</summary>
    private ISpecSetPublisher Publisher() => new SpecSetPublisher(
        Writer,
        new SpecSetPointerRecorder(Pointers, NullLogger<SpecSetPointerRecorder>.Instance),
        PullRequests,
        new SpecRefusalReporter(new NoOpEventPublisher(), NullLogger<SpecRefusalReporter>.Instance),
        Mock.Of<Contracts.Persistence.IRunArtifactStore>(),
        NullLogger<SpecSetPublisher>.Instance);

    internal DeriveSpecContext Context(Ticket ticket, string? carriedJson = null)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, "branch-sha"));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        pipeline.Set(ContextKeys.TrackerPlatform, "azdo");
        pipeline.Set(ContextKeys.TrackerConnection, ApprovedSets.Tracker);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { [Repo] = sandbox.Object });
        pipeline.Set(ContextKeys.Ticket, ticket);
        if (carriedJson is not null) pipeline.Set(ContextKeys.ApprovedSpecSet, carriedJson);
        return new DeriveSpecContext(
            ticket, null, [new RepoConnection { Name = Repo }], new AgentConfig(), pipeline);
    }

    internal sealed class RecordingSpecSetWriter : ISpecSetWriter
    {
        internal SpecSet? Written { get; private set; }

        internal bool Fail { get; set; }

        public Task<SpecSetWriteResult> WriteAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set, CancellationToken ct)
        {
            Written = set;
            return Task.FromResult(Fail
                ? SpecSetWriteResult.Failed("the push was rejected")
                : SpecSetWriteResult.Ok("revision-sha"));
        }
    }

    internal sealed class RecordingPullRequestOpener : ISpecPullRequestOpener
    {
        internal int Opened { get; private set; }

        public Task<string?> OpenAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, SpecSet set, CancellationToken ct)
        {
            Opened++;
            return Task.FromResult<string?>("https://example.invalid/pr/1");
        }
    }

    internal sealed class CountingDeriver : ISpecSetDeriver
    {
        internal int Calls { get; private set; }

        internal SpecDerivation? Result { get; set; }

        public Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
            Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string cause,
            AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<(SpecDerivation?, string?)>((Result, Result is null ? "no cut" : null));
        }
    }
}
