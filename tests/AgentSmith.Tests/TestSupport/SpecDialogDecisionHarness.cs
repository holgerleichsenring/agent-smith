using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.References;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-11-10-ccd9: the outcome flow, the decision path, the router and the dialog read over a test's store —
/// the proposal awaiting a decision is stored on the row, shown, and decided later by id.
/// </summary>
internal sealed class SpecDialogDecisionHarness(
    AgentSmithDbContext context, DashboardOutcomeChannel? channel = null, IOutcomeSink? sink = null,
    SpecDialogMessenger? messenger = null, IEnumerable<IChatThreadAdapter>? threads = null)
{
    public SpecDialogSessionRepository Repository { get; } = new(context);
    public SpecDialogSessionManager Sessions => new(Repository, BoundSessionNudges.Quiet(Repository),
        AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
    public SpecDialogProposalState Proposals =>
        new(new SpecDialogProposalClaim(context), Repository, BoundSessionNudges.Quiet(Repository));
    public SpecDialogMessenger Messenger { get; } = messenger ?? new([], NullLogger<SpecDialogMessenger>.Instance);
    public DashboardOutcomeChannel Channel { get; } = channel ?? new(
        new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()), NullLogger<DashboardOutcomeChannel>.Instance);
    public IOutcomeSink Sink { get; } = sink ?? Mock.Of<IOutcomeSink>();
    public SpecDialogLatestOutcomeStore Outcomes =>
        new(Repository, BoundSessionNudges.Quiet(Repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance);

    public SpecDialogChatCard Cards => new(new ChatThreadAdapters(threads ?? [], NullLogger<ChatThreadAdapters>.Instance),
        Messenger, new SpecDialogOutcomeComposer(), Proposals, NullLogger<SpecDialogChatCard>.Instance);

    public SpecDialogNotices Notices => new(Messenger, Sessions);

    public SpecDialogOutcomeFlow Flow() => new(Proposals, new SpecDialogOutcomeComposer(), new SpecDialogReplyComposer(),
        Notices, Channel, Cards, NullLogger<SpecDialogOutcomeFlow>.Instance);

    public SpecDialogDecisionPath Decisions() => new(Proposals, Outcomes, Sink, RatifiedMockStamps.Over(Repository),
        new SpecDialogOutcomeComposer(), Notices, Cards);

    public SpecDialogRouter Router(
        SpecDialogCommandHandler commands, ISpecDialogTurnRunner turns, SpecDialogTurnGate gate,
        SpecDialogAnswerAdmission admission, SpecDialogTurnKeeper keeper, SpecDialogSessionManager? sessions = null) =>
        new(new SpecCommandParser(), commands, turns, Flow(), Decisions(), gate, admission, keeper,
            new SpecDialogReplyComposer(), Messenger, NullLogger<SpecDialogRouter>.Instance);

    public SpecDialogViewReader View(
        SpecDialogSessionManager sessions, SpecDialogProjectCatalog projects, SpecDialogPendingQuestions pending,
        SpecDialogProposalComposer composer, SpecDialogTurnGate turns, SpecDialogTicketTextRepository ticketText,
        ConversationUploads uploads, AgentSmith.Contracts.Sandbox.IConversationMockReader? mocks = null) =>
        new(sessions, projects, pending, Outcomes, Proposals, Cards, composer, turns,
            new SpecDialogHoldings(ticketText, uploads, mocks));

    public Task<ConversationState> OpenAsync(string platform, string thread, CancellationToken ct = default) =>
        Sessions.OpenAsync(platform, "C1", thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, ct);
}
