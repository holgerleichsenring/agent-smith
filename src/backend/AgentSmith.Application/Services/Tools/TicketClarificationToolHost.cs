using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0315d: the ask_human surface for TICKET-triggered runs (phase-execution).
/// There is no live dialogue transport on an ephemeral run container, so a
/// question cannot block for an answer — instead the FIRST question is
/// captured here and the tool tells the master to stop; after the loop the
/// MasterOpenQuestions step posts it as a p0318 open-questions ticket comment
/// and parks the ticket in needs_clarification_status. The operator's
/// answering comment plus the status move re-trigger a fresh run whose
/// prompt carries the answers (PlanAnswers).
/// </summary>
public sealed class TicketClarificationToolHost : IToolHost
{
    /// <summary>The first question the master asked this run, if any.</summary>
    public PlanOpenQuestion? Captured { get; private set; }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(AskHuman, name: "ask_human")];
    }

    [Description("Asks the human operator for guidance. On this run the question is posted to the ticket and the run pauses until the operator answers — only ask when you are genuinely blocked.")]
    public string AskHuman(
        [Description("Question text to display to the human.")] string question,
        [Description("Optional context block shown alongside the question.")] string? context = null,
        [Description("Optional list of choices. Each entry is {label, description?}.")] IReadOnlyList<HumanToolHost.AskHumanChoice>? choices = null,
        [Description("Optional 0-based index into choices identifying the recommended option.")] int? recommended_index = null)
    {
        _ = recommended_index;
        if (Captured is not null)
            return "A question is already posted to the ticket and this run is pausing "
                + "for the operator's answer. Stop now — end your reply.";

        var text = string.IsNullOrWhiteSpace(context) ? question : $"{question}\n\n{context}";
        Captured = new PlanOpenQuestion(
            "1", text, choices?.Select(c => c.label).ToList() ?? []);
        return "This run cannot receive a live answer. Your question will be posted to the "
            + "ticket and the run pauses until the operator answers; the answer re-triggers "
            + "a fresh run. STOP now: make no further changes, emit no verdict, and end your "
            + "reply with a one-line note that you are waiting for the answer.";
    }
}
