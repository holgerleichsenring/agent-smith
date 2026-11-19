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
/// as it is — until somebody else commits to a scoped branch. Then it is written again: 2026-11-19-6b0ba, as a
/// stand-in its coding master writes, and the set stays approved.
/// </summary>
public sealed class HandbackExpiryTests
{
    private const string Key = "azdo-1001";
    private const string Repo = "primary";
    private const string Series = "2026-10-12-0a0a";
    private static readonly SpecApproval Approval = new(DateTimeOffset.UtcNow.AddDays(-1), "conv-1", "reviewer");

    [Fact]
    public async Task Expiry_ApprovedSet_StandInStaysApproved()
    {
        var published = new CapturingPublisher();

        await Handler(published).ExecuteAsync(Context(author: "reviewer@example.com"), default);

        published.Set!.IsHandedBack.Should().BeFalse("a foreign commit expires the stored hand-back");
        published.Set.Specs.Should().ContainSingle();
        published.Set.StandIns.Should().ContainSingle("its coding master writes the spec again (2026-11-19-6b0ba)");
        published.Set.Approval.Should().Be(Approval, "the approval rides onto the set written again");
    }

    [Fact]
    public async Task Expiry_OnlyOurCommitsSince_ReplaysTheStoredHandback()
    {
        var published = new CapturingPublisher();
        var context = Context(author: SandboxGitIdentity.Email);

        await Handler(published).ExecuteAsync(context, default);

        published.Set!.IsHandedBack.Should().BeTrue();
        context.Pipeline.TryGet<bool>(ContextKeys.ForeignCommitSinceHandback, out var foreign).Should().BeTrue();
        foreign.Should().BeFalse();
    }

    private static DeriveSpecHandler Handler(ISpecSetPublisher publisher)
    {
        var pointers = new InMemorySpecSetPointerStore();
        pointers.SaveAsync(string.Empty, new SpecSetPointer(
                Key, Repo, "spec-sha", 1, SpecHandbackCase.RequirementsContradictRepository, 1, Series,
                HandbackHeads: new Dictionary<string, string> { [Repo] = "stored-sha" }), default)
            .GetAwaiter().GetResult();
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var standIns = new SpecStandIns(validator, new SpecDraftReader(), new DerivedSpecYamlRenderer());
        var tickets = new Mock<ITicketProviderFactory>();
        return new DeriveSpecHandler(
            SeriesDoubles.BranchRead(new StubReader()), publisher, pointers,
            new ApprovedSpecSetResolver(new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance),
            standIns,
            new SpecSetTicketCommenter(tickets.Object, Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(),
                AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(tickets.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            HandbackTestDoubles.Probe(), new SpecSetUnchanged(new SeriesFiles(new SeriesManifest())));
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
            PipelineContext pipeline, RepoConnection carryingRepo, IReadOnlyList<RepoConnection> repositories,
            TicketKey ticket, CancellationToken ct) =>
            Task.FromResult(SpecSetOnBranch.Answered(new SpecSetReadResult(StoredHandback(), "spec-sha")));
    }


    private sealed class CapturingPublisher : ISpecSetPublisher
    {
        public SpecSet? Set { get; private set; }

        public Task<CommandResult> PublishAsync(
            PipelineContext pipeline, string project, SeriesScope scope, SpecSet set,
            IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken ct)
        {
            Set = set;
            return Task.FromResult(CommandResult.Ok("published"));
        }

        public bool Kept { get; private set; }

        public Task<CommandResult> KeepAsync(
            PipelineContext pipeline, SeriesScope scope, SpecSet set, string revisionSha, CancellationToken ct)
        {
            (Set, Kept) = (set, true);
            return Task.FromResult(CommandResult.Ok("kept"));
        }
    }
}
