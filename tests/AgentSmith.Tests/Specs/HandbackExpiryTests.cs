using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Handlers;
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
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-12-3a36b: an approved set the branch carries as a CONTRADICTION hand-back is replayed
/// without a model call — until somebody else commits to a scoped branch. Then it is derived again,
/// the derived set stays approved, and a derivation that yields nothing keeps the stored hand-back
/// rather than an approved one-phase guess.
/// </summary>
public sealed class HandbackExpiryTests
{
    private const string Key = "azdo-1001";
    private const string Repo = "primary";
    private const string Series = "2026-10-12-0a0a";
    private static readonly SpecApproval Approval = new(DateTimeOffset.UtcNow.AddDays(-1), "conv-1", "reviewer");

    [Fact]
    public async Task Expiry_ApprovedSet_ReDerivedSetStaysApproved()
    {
        var deriver = new StubDeriver(Derived());
        var published = new CapturingPublisher();

        await Handler(deriver, published).ExecuteAsync(Context(author: "reviewer@example.com"), default);

        deriver.Calls.Should().Be(1, "a foreign commit expires the stored hand-back");
        published.Set!.IsHandedBack.Should().BeFalse();
        published.Set.Phases.Should().ContainSingle();
        published.Set.Approval.Should().Be(Approval, "the approval rides onto the set derived again");
    }

    [Fact]
    public async Task Expiry_DerivationFails_KeepsHandbackNoApprovedFallback()
    {
        var deriver = new StubDeriver(null);
        var published = new CapturingPublisher();

        await Handler(deriver, published).ExecuteAsync(Context(author: "reviewer@example.com"), default);

        deriver.Calls.Should().Be(1);
        published.Set!.IsHandedBack.Should().BeTrue("expiry never approves a guess");
        published.Set.Phases.Should().BeEmpty("no one-phase fallback is created");
    }

    [Fact]
    public async Task Expiry_OnlyOurCommitsSince_ReplaysTheStoredHandbackWithoutTheModel()
    {
        var deriver = new StubDeriver(Derived());
        var published = new CapturingPublisher();
        var context = Context(author: SandboxGitIdentity.Email);

        await Handler(deriver, published).ExecuteAsync(context, default);

        deriver.Calls.Should().Be(0);
        published.Set!.IsHandedBack.Should().BeTrue();
        context.Pipeline.TryGet<bool>(ContextKeys.ForeignCommitSinceHandback, out var foreign).Should().BeTrue();
        foreign.Should().BeFalse();
    }

    private static DeriveSpecHandler Handler(ISpecSetDeriver deriver, ISpecSetPublisher publisher)
    {
        var pointers = new InMemorySpecSetPointerStore();
        pointers.SaveAsync(string.Empty, new SpecSetPointer(
                Key, Repo, "spec-sha", 1, SpecHandbackCase.RequirementsContradictRepository, 1, Series,
                HandbackHeads: new Dictionary<string, string> { [Repo] = "stored-sha" }), default)
            .GetAwaiter().GetResult();
        var validator = new SpecDraftValidator(new PhaseSpecSchemaProvider());
        var fallback = new SpecFallback(validator, new PhaseDraftReader(), new DerivedPhaseYamlRenderer());
        var gate = new SpecCutGate(new NoOpEventPublisher(), NullLogger<SpecCutGate>.Instance);
        var tickets = new Mock<ITicketProviderFactory>();
        return new DeriveSpecHandler(
            deriver, new StubReader(), publisher, pointers,
            new ApprovedSpecSetResolver(new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                NullLogger<SpecSourceResolver>.Instance),
            fallback, new SpecCoverageRefusal(gate, fallback),
            new SpecSetTicketCommenter(tickets.Object, Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(),
                NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(), gate,
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(tickets.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            HandbackTestDoubles.Probe(), NullLogger<DeriveSpecHandler>.Instance);
    }

    private static DeriveSpecContext Context(string author)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { [Repo] = new GitSandbox(author) });
        pipeline.Set(ContextKeys.Ticket, Ticket());
        return new DeriveSpecContext(Ticket(), null, [new RepoConnection { Name = Repo }], new AgentConfig(), pipeline);
    }

    private static Ticket Ticket() =>
        new(new TicketId("1001"), "Send the shipment id", "Send the shipment id with the assets.", null, "open", "azdo", []);

    private static SpecSet StoredHandback() => new(
        Key, [], SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow.AddHours(-2))],
        SpecSource.BranchArtifact,
        new SpecHandback(SpecHandbackCase.RequirementsContradictRepository, "the request has no such field"),
        TicketFingerprint: TicketTextFingerprint.Of(Ticket()), Approval: Approval, Series: Series);

    private static SpecDerivation Derived()
    {
        var segments = TicketSegmenter.Segment(Ticket().Description);
        var id = SeriesIdFactory.Member(Series, 0);
        SpecPhase[] phases =
        [
            new(new PhaseDraft(id, "Send the id", $"spec: {id}", []) { Done = ["The id is sent."] },
                id, string.Empty, [.. segments.Select(s => s.Id)]),
        ];
        return new SpecDerivation(
            new SpecSet(Key, phases, SpecAccountingBuilder.Build(phases, [], segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
                SpecSource.BranchArtifact, Series: Series),
            []);
    }

    /// <summary>Answers the probe's git questions: our identity, main as the base, and one commit
    /// since the stored head by <c>author</c>.</summary>
    private sealed class GitSandbox(string author) : ISandbox
    {
        public string JobId => "expiry";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            var output = step.Args?.FirstOrDefault() switch
            {
                "config" => SandboxGitIdentity.Email,
                "symbolic-ref" => "origin/main",
                "rev-list" => $"commit 0123abcd\n{author}\n",
                _ => string.Empty,
            };
            return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0, null, output));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubReader : ISpecSetReader
    {
        public Task<SpecSetOnBranch> ReadAsync(
            PipelineContext pipeline, RepoConnection carryingRepo, TicketKey ticket, CancellationToken ct) =>
            Task.FromResult(SpecSetOnBranch.Answered(new SpecSetReadResult(StoredHandback(), "spec-sha")));
    }

    private sealed class StubDeriver(SpecDerivation? result) : ISpecSetDeriver
    {
        public int Calls { get; private set; }

        public Task<(SpecDerivation? Derivation, string? Error)> DeriveAsync(
            Ticket ticket, IReadOnlyList<TicketSegment> segments, SpecSet? previous, string series,
            string cause, IReadOnlyList<string> repositories, AgentConfig agentConfig, PipelineContext pipeline,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<(SpecDerivation?, string?)>((result, result is null ? "no cut" : null));
        }
    }

    private sealed class CapturingPublisher : ISpecSetPublisher
    {
        public SpecSet? Set { get; private set; }

        public Task<CommandResult> PublishAsync(
            PipelineContext pipeline, string project, RepoConnection carryingRepo, SpecSet set,
            IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken ct)
        {
            Set = set;
            return Task.FromResult(CommandResult.Ok("published"));
        }
    }
}
