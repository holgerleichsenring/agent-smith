using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the PlanOpenQuestionsHandler step. Reads the typed Plan from
/// PipelineContext (set by GeneratePlanHandler when the strict path produces one)
/// and posts a structured open-questions comment to the originating ticket
/// when the Plan declared status=needs_user_input.
/// </summary>
public sealed record PlanOpenQuestionsContext(
    Ticket Ticket,
    TicketConfig TicketConfig,
    PipelineContext Pipeline) : ICommandContext;
