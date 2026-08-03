using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0315d, p0393a: renders the phase user prompt for the coding master — the prompt of
/// every code run, because every code run now carries a derived phase spec.
/// <para>
/// The validated spec travels verbatim inside the untrusted-ticket delimiters (it came
/// from a ticket body — p0316 rule); the contract around it — decisions get logged,
/// tests run, every done criterion is verified before the verdict — is framework text
/// OUTSIDE the markers. p0393a: the spec's steps are NOT the plan. GeneratePlan derives
/// the plan from the spec against the actual codebase and it arrives in the system
/// prompt; the spec states what must become true, and the phase's markdown companion
/// carries the ticket's own naming rules and templates byte-identical.
/// </para>
/// Operator answers from a prior clarification park (PlanAnswers, re-triggered run) are
/// rendered authoritative.
/// </summary>
public sealed class PhaseExecutionPromptFactory : IPhaseExecutionPromptFactory
{
    public string Build(
        PipelineContext pipeline, Ticket ticket, Repository repository, IEnumerable<string> sandboxKeys,
        string conversationSection, string attachmentsSection)
    {
        // Absent spec is a composition bug — PhaseSpecGate runs before the master.
        var draft = pipeline.Get<PhaseDraft>(ContextKeys.PhaseSpec);
        var answers = pipeline.TryGet<Dictionary<string, string>>(ContextKeys.PlanAnswers, out var a)
            ? a : null;

        var specBlock = TicketPromptDelimiters.Wrap($"""
            **ID:** {ticket.Id}
            **Title:** {ticket.Title}

            ```yaml
            {draft.Yaml.Trim()}
            ```
            """);

        // p0317: conversation + attachments follow the spec block — on a run
        // re-triggered after a clarification park, the hydrated comment thread
        // (including a comment posted while the ticket was still parked) is the
        // operator's answer trail; both sections arrive pre-delimited.
        var header = string.Join("\n\n",
            new[] { specBlock, conversationSection, attachmentsSection }
                .Where(s => !string.IsNullOrEmpty(s)));

        return $"""
            {header}

            ## Working Repository
            **Path:** {repository.LocalPath}
            **Branch:** {repository.CurrentBranch}
            **Sandbox keys:** {string.Join(", ", sandboxKeys)}

            ## Phase contract
            You are implementing PHASE {draft.PhaseId} — this phase and nothing after it.
            The spec above states what must become true; the plan in your instructions states
            how, derived against this codebase. Log every non-obvious choice via log_decision
            as you go. Where the spec names tests, write them; run the repository's build and
            automated tests as usual.
            {BuildDoneCriteriaSection(draft)}
            Before you emit your verdict, verify EACH done criterion above out loud —
            a criterion you cannot satisfy makes the run red, not silently smaller.
            When you are genuinely blocked on missing input, call ask_human once: the
            question is posted to the ticket and this run pauses for the answer.
            {AgentPromptBuilder.BuildOperatorAnswersSection(answers)}
            """;
    }

    private static string BuildDoneCriteriaSection(PhaseDraft draft)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var done = (OutcomeYamlReader.GetList(map, "done") ?? [])
            .Select(d => d?.ToString())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToList();
        if (done.Count == 0) return string.Empty;
        var bullets = string.Join("\n", done.Select(d => $"- {d}"));
        return $"\n### Done criteria\n{bullets}\n";
    }
}
