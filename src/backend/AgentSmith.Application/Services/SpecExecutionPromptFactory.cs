using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0315d, p0393a: renders the spec user prompt for the coding master — the prompt of
/// every code run, because every code run now carries a derived spec.
/// <para>
/// The validated spec travels verbatim inside the untrusted-ticket delimiters (it came
/// from a ticket body — p0316 rule); the contract around it — decisions get logged,
/// tests run, every done criterion is verified before the verdict — is framework text
/// OUTSIDE the markers. p0394a: the spec IS the plan of record. Its steps seed the
/// progress ledger the master opens on; the spec's markdown companion carries the
/// ticket's own naming rules and templates byte-identical.
/// </para>
/// Operator answers from a prior clarification park (PlanAnswers, re-triggered run) are
/// rendered authoritative.
/// <para>2026-10-13-8762m: the build notes of the specs that already ran and the memory index, in code —
/// no master carries the index placeholder, so the user prompt is where the master reads it.</para>
/// </summary>
public sealed class SpecExecutionPromptFactory : ISpecExecutionPromptFactory
{
    public string Build(
        PipelineContext pipeline, Ticket ticket, Repository repository, IEnumerable<string> sandboxKeys,
        string conversationSection, string attachmentsSection)
    {
        // Absent spec is a composition bug — SpecGate runs before the master.
        var draft = pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec);
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
            {SpecExecutionPromptBlocks.StagedRegistries(pipeline)}
            {SpecExecutionPromptBlocks.OutstandingCriteria(pipeline)}
            {SpecExecutionPromptBlocks.DeclaredGate(pipeline)}
            {SpecExecutionPromptBlocks.StructureFindings(pipeline)}
            {SpecExecutionPromptBlocks.StaleCitations(pipeline)}
            {Specs.SpecBuildNoteSection.Render(Specs.PrecedingSpecs.NotesOf(pipeline, draft.SpecId), set: pipeline.TryGet<Contracts.Specs.SpecSet>(ContextKeys.SpecSet, out var set) ? set : null)}
            {Handlers.MasterPromptSections.BuildMemoryIndexSection(pipeline)}

            ## Spec contract
            You are implementing SPEC {draft.SpecId} — this spec and nothing after it.
            The spec above is the plan of record: its steps are your checklist (the progress
            ledger is seeded from them) and you own how each step lands in this codebase.
            Take a choice a person might overrule with decide_by_default; log every other non-obvious choice via log_decision
            as you go. Where the spec names tests, write them; run the repository's build and automated tests as usual.
            {SpecExecutionPromptBlocks.DoneCriteria(draft)}
            Before you emit your verdict, verify EACH done criterion above out loud —
            a criterion you cannot satisfy makes the run red, not silently smaller.
            Before the verdict, call hand_forward once with what you renamed and moved (each
            `<old> -> <new>`), persisted and deviated from: the next spec's master reads it.
            When you are genuinely blocked on missing input, call ask_human once: the
            question is posted to the ticket and this run pauses for the answer.
            {AgentPromptBuilder.BuildOperatorAnswersSection(answers)}
            """;
    }

}
