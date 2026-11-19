using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
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

    /// <summary>2026-11-19-6b0ba: what took the deriver's place — whether a run cut the set (wrote stand-ins or
    /// handed an input over kept specs to their master), and under which cause.</summary>
    internal CutProbe Recuts { get; } = new();

    /// <summary>2026-09-17-0e79b: the kept-set notices posted to the ticket.</summary>
    internal RecordingTicketComments Notices { get; } = new();

    /// <summary>2026-09-17-0e79b: the cut comments posted to the ticket, which is what moves the
    /// anchor the comment rule measures from.</summary>
    internal RecordingTicketComments Cuts { get; } = new();

    /// <summary>2026-09-17-0e79b: the run decisions the notice wrote.</summary>
    internal RecordingRunDecisions Decisions { get; } = new();

    internal string? PointerSha { get; set; }

    /// <summary>Set to <see cref="RecordingTicketComments.Tracker"/> to give the run a tracker to
    /// comment on; null (the default) is a run with none, which posts nothing.</summary>
    internal TrackerConnection? Tracker { get; set; }

    /// <summary>The thread the run carries, which is what the comment rule reads.</summary>
    internal IReadOnlyList<TicketComment>? Comments { get; set; }

    internal ICommandHandler<DeriveSpecContext> Handler() => new ProbedHandler(Build(), Recuts);

    private DeriveSpecHandler Build()
    {
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var draftReader = new SpecDraftReader();
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(Branch);
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance, factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        if (PointerSha is { } sha)
            Pointers.SaveAsync(string.Empty, new SpecSetPointer(Branch.Key, Repo, sha, 1), default)
                .GetAwaiter().GetResult();
        var cuts = Cuts.Factory();
        return new DeriveSpecHandler(
            SeriesDoubles.BranchRead(SeriesDoubles.Reader(factory.Object, gitOps), factory.Object),
            Publisher(),
            Pointers,
            new ApprovedSpecSetResolver(Approvals, NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance),
            new SpecStandIns(validator, draftReader, new DerivedSpecYamlRenderer()),
            new SpecSetTicketCommenter(cuts, Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetTicketCommenter>.Instance),
            new ApprovedSetKeptNotice(
                Notices.Factory(), Decisions, NullLogger<ApprovedSetKeptNotice>.Instance),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(cuts, NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Probe(),
            new SpecSetUnchanged(new SeriesFiles(new SeriesManifest())));
    }

    /// <summary>The real publisher: the writer, the pointer recorder and the draft pull request.</summary>
    private ISpecSetPublisher Publisher() => new SpecSetPublisher(
        Writer,
        new SeriesRepos(),
        new SpecSetPointerRecorder(Pointers, NullLogger<SpecSetPointerRecorder>.Instance),
        PullRequests,
        new SpecRefusalReporter(new NoOpEventPublisher(), NullLogger<SpecRefusalReporter>.Instance),
        new SpecSetContextPublication(Mock.Of<Contracts.Persistence.IRunArtifactStore>(), NullLogger<SpecSetContextPublication>.Instance),
        new SpecSetAnnouncer(new NoOpEventPublisher(), NullLogger<SpecSetAnnouncer>.Instance),
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
        if (Comments is not null) pipeline.Set(ContextKeys.TicketComments, Comments);
        return new DeriveSpecContext(
            ticket, Tracker, [new RepoConnection { Name = Repo }], new AgentConfig(), pipeline);
    }

    internal sealed class RecordingSpecSetWriter : ISpecSetWriter
    {
        internal SpecSet? Written { get; private set; }

        internal bool Fail { get; set; }

        public Task<SpecSetWriteResult> WriteAsync(
            PipelineContext pipeline, SeriesPlacement placement, CancellationToken ct)
        {
            Written = placement.Set;
            return Task.FromResult(Fail
                ? SpecSetWriteResult.Failed("the push was rejected")
                : SpecSetWriteResult.Ok(
                    placement.Names.ToDictionary(n => n, _ => "revision-sha"), "revision-sha"));
        }
    }

    internal sealed class RecordingPullRequestOpener : ISpecPullRequestOpener
    {
        internal int Opened { get; private set; }

        public Task<IReadOnlyDictionary<string, string>> OpenAsync(
            PipelineContext pipeline, SeriesPlacement placement, SeriesScope scope, CancellationToken ct)
        {
            Opened++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { [placement.Carrier?.Name ?? string.Empty] = "https://example.invalid/pr/1" });
        }
    }

    /// <summary>Counts the runs that cut the set and records the cause the last one ran under.</summary>
    internal sealed class CutProbe
    {
        internal int Calls { get; set; }

        internal string? CauseSeen { get; set; }
    }

    private sealed class ProbedHandler(DeriveSpecHandler inner, CutProbe cuts) : ICommandHandler<DeriveSpecContext>
    {
        public async Task<CommandResult> ExecuteAsync(DeriveSpecContext context, CancellationToken cancellationToken)
        {
            var result = await inner.ExecuteAsync(context, cancellationToken);
            var set = context.Pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var published) ? published : null;
            if (context.Pipeline.Has(ContextKeys.SpecKeptInput) || set is { StandIns.Count: > 0 })
            {
                cuts.Calls++;
                cuts.CauseSeen = set?.Current.Cause;
            }
            return result;
        }
    }
}
