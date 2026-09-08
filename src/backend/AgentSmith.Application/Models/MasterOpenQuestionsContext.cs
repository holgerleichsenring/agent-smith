using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0315d: context for the MasterOpenQuestions step. Reads the question the
/// executing master captured mid-run (ContextKeys.MasterOpenQuestions, set by
/// AgenticMasterHandler from TicketClarificationToolHost) and posts it to the
/// originating ticket via the p0318 open-questions transport.
/// 2026-09-03-3c07: <paramref name="Step"/> is the command being executed — the
/// re-engaged master a resumed run splices belongs to the same phase as this step.
/// </summary>
public sealed record MasterOpenQuestionsContext(
    Ticket Ticket,
    TrackerConnection TrackerConnection,
    PipelineContext Pipeline,
    PipelineCommand Step) : ICommandContext;
