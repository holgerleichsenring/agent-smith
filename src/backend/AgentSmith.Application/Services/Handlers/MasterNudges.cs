using AgentSmith.Application.Extensions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Progress;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The prompts the run sends BACK to a master that stopped short — the apply nudge,
/// the re-engagement nudge and the in-pass reminder. Pure
/// text over ledger state; p0403 lifted them out of the handler.
/// </summary>
internal static class MasterNudges
{
    // p0255: the focused second-shot prompt when the master planned but edited
    // nothing — the plan is not the deliverable, the edited source is.
    internal static string BuildApplyNudge(string originalUserPrompt, ProgressLedger ledger) =>
        "You wrote a plan and have not made the changes it names yet. The plan is not the "
        + "deliverable; the change is. Make those edits, build and run the tests, then close with your report for the "
        + "person. If an edit cannot be made, the report says what is missing and why. " + ReportAgain + "\n\n"
        + LedgerNudgeSection(ledger)
        + "Original task:\n" + originalUserPrompt;

    // p0341: a re-drive starts a fresh loop, so carry the ledger forward from
    // PipelineContext (done vs remaining) — the salvage pass resumes the checklist
    // instead of restarting blind. Empty ledger (no plan) contributes nothing.
    internal static string LedgerNudgeSection(ProgressLedger ledger) =>
        ledger.IsEmpty ? string.Empty : ProgressLedgerRenderer.Render(ledger) + "\n\n";

    // p0341c: the WARM re-engagement nudge — the current ledger (the checklist / coverage)
    // PLUS a working-state block (decisions so far + last build/test tail — the continuity),
    // so a resumed pass carries WHAT WAS LEARNED, not only WHAT REMAINS.
    // p0411: the state block also carries the changed paths the framework just read from
    // the sandboxes, so re-orientation is an answer the pass opens with.
    internal static string BuildReengageNudge(
        string originalUserPrompt, ProgressLedger ledger,
        IReadOnlyList<PlanDecision> decisions,
        IReadOnlyList<string>? changedPaths = null,
        IReadOnlyList<string>? stagedRegistries = null) =>
        // p0341f: the instruction to "resume from where you left off, do not restart from
        // scratch" is gone. It was asked of a pass that had been handed no left-off, so the
        // only way to obey it was to re-derive — 34 passes of the same greps on run 98b9.
        // The transcript now arrives with the request; the nudge names the next turn.
        // 2026-11-22-96c8a: a re-drive happens only over open steps; what it ends on is added to the report.
        "These checklist steps are still open. Do the ones that hold work you have not done, "
        + "and mark a step done or remove it when it holds none. If a step needs a decision "
        + "only the operator can make, ask_human posts it; this pass ends and the next starts with "
        + "the answer. " + ReportAgain + "\n\n"
        + LedgerNudgeSection(ledger)
        + WorkingStateSection.Build(decisions, changedPaths, stagedRegistries)
        + "Original task:\n" + originalUserPrompt;

    /// <summary>2026-11-22-96c8a: what a pass ends on is added to the report — say what this pass did.</summary>
    internal const string ReportAgain =
        "Close with your report of what this pass did; it is added to what you reported before.";

    /// <summary>2026-11-22-96c8a: the one drive a step gets when none of its passes ended on text (the old verdict
    /// nudge's place) — a worker's empty closing turn, a final reply that was only a call.</summary>
    internal const string AskForReport = "Your last reply had no text. Write your report for the person as your reply.";

    // p0341c/p0359: the in-pass reminder, injected when the ledger went STALE (N
    // iterations without an update_progress call) or on drift. Styled after an
    // interactive harness's todo reminder: gentle, states that restructuring is
    // allowed (the plan may have deviated), and explicitly ignorable when the
    // shown state is still accurate — a nag the model can dismiss beats one it
    // learns to tune out.
    internal static string BuildInPassReminder(ProgressLedger ledger)
    {
        if (ledger.IsEmpty)
            return "<system-reminder>\n"
                + "The progress ledger is empty and the update_progress tool has not been used "
                + "recently. If you are doing multi-step work, seed the checklist from your plan "
                + "now — it is your durable memory across this run. If the task is genuinely "
                + "trivial, ignore this reminder.\n"
                + "</system-reminder>";
        if (ledger.ActionablePending.Count == 0)
            // p0391: the invitation is QUALIFIED. Unqualified, "add those steps" sat next to a
            // mechanism that turns any added item into another loop pass — and a model that
            // keeps appending re-verification items re-drives itself until the money or the
            // operator stops it. Adding real, unstarted work is still right; re-reading what is
            // already recorded is not work, it is the run being finished.
            return "<system-reminder>\n"
                + "Every step in the progress ledger is marked done. If the work is truly "
                + "complete, verify (build + tests) and close with your report as your reply's text. Add a step only for "
                + "work you have not done yet and that the checklist does not cover — a step "
                + "that would only re-read or re-confirm evidence you already recorded is not "
                + "remaining work, it means you are done: close with your report as your reply's text.\n"
                + "</system-reminder>";
        return "<system-reminder>\n"
            + "The update_progress tool has not been used recently. If you completed steps, mark "
            + "them done; flip the step you are working on to in_progress. If the plan has "
            + "evolved, restructure the checklist (add, reword, or remove steps — full-state "
            + "replace) so it reflects what you are actually doing. Current recorded state:\n"
            + ProgressLedgerRenderer.Render(ledger) + "\n"
            + "If this is still accurate and you are mid-step, ignore this reminder.\n"
            + "</system-reminder>";
    }
}
