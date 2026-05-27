using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for writing run result to <c>.agentsmith/runs/</c>.
///
/// Two modes:
/// - Ticket-driven (fix-bug / add-feature / mad-discussion / security-scan / api-scan):
///   <see cref="Plan"/>, <see cref="Ticket"/>, <see cref="Changes"/> all populated;
///   handler writes plan.md + result.md grounded on the ticket.
/// - Init mode (init-project): <see cref="Plan"/> and <see cref="Ticket"/> are
///   <c>null</c> and <see cref="Changes"/> is empty. The bootstrap skill writes
///   the deliverable files (<c>.agentsmith/context.yaml</c>, coding-principles.md)
///   directly; this handler persists bootstrap.md (when present in
///   <c>ContextKeys.BootstrapMarkdown</c>) plus an init-mode result.md and
///   appends the run entry to <c>context.yaml</c>.
/// </summary>
public sealed record WriteRunResultContext(
    Repository Repository,
    Plan? Plan,
    Ticket? Ticket,
    IReadOnlyList<CodeChange> Changes,
    PipelineContext Pipeline) : ICommandContext;
