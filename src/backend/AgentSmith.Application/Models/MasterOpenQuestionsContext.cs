using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0315d: context for the MasterOpenQuestions step. Reads the question the
/// executing master captured mid-run (ContextKeys.MasterOpenQuestions, set by
/// AgenticMasterHandler from TicketClarificationToolHost) and posts it to the
/// originating ticket via the p0318 open-questions transport.
/// </summary>
public sealed record MasterOpenQuestionsContext(
    Ticket Ticket,
    TrackerConnection TrackerConnection,
    PipelineContext Pipeline) : ICommandContext;
