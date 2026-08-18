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
/// The prompts the run sends BACK to a master that stopped short — the verdict
/// nudge, the apply nudge, the re-engagement nudge and the in-pass reminder. Pure
/// text over ledger + verification state; p0403 lifted them out of the handler.
/// </summary>
internal static class MasterNudges
{
    // p0263: the focused second-shot prompt when the master edited source but emitted
    // no verdict — verify only (no further edits) and emit ONLY the verdict block.
    internal static string BuildVerdictNudge(string originalUserPrompt, ProgressLedger ledger) =>
        "Your previous pass changed source but did NOT emit the required Phase 4 verdict, "
        + "so the run cannot be reported. Do NOT make further code changes now. Build the "
        + "project and run the automated tests the way the repository defines them, then emit "
        + "ONLY your final fenced ```verdict block reflecting the real build/test outcome "
        + "(status: green | no-tests | failed). Nothing before or after the block.\n\n"
        + LedgerNudgeSection(ledger)
        + "Original task:\n" + originalUserPrompt;

    // p0255: the focused second-shot prompt when the master planned but edited
    // nothing — the plan is not the deliverable, the edited source is.
    internal static string BuildApplyNudge(string originalUserPrompt, ProgressLedger ledger) =>
        "You wrote a plan but have NOT edited any source file yet. The plan is not the "
        + "deliverable — the edited source is. Apply your plan NOW: make the edits with "
        + "edit / multi_edit / write_file (repo-prefixed paths), then build, run the tests, "
        + "and emit your verdict. Do not stop until at least one SOURCE file is changed, or "
        + "you report a concrete blocker explaining why no edit was possible.\n\n"
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
        IReadOnlyList<PlanDecision> decisions, MasterVerification? verification,
        IReadOnlyList<string>? changedPaths = null,
        IReadOnlyList<string>? stagedRegistries = null) =>
        // p0363: a red verdict with open checklist items gets an explicit persistence
        // lead-in — the failing build IS the current step, not a reason to stop.
        (verification?.Status == VerificationStatus.Failed
            ? "Your last verification came back RED — and your own checklist still has "
              + "actionable steps for exactly that (fixing the build/tests IS the current "
              + "step). Reporting the failure is not completing the step: keep working the "
              + "checklist, and mark steps done as they actually pass. Only stop if you can "
              + "justify concretely why the remaining steps cannot succeed.\n\n"
            : "")
        // p0341f: the instruction to "resume from where you left off, do not restart from
        // scratch" is gone. It was asked of a pass that had been handed no left-off, so the
        // only way to obey it was to re-derive — 34 passes of the same greps on run 98b9.
        // The transcript now arrives with the request; the nudge names the next turn.
        + "Continue the checklist — these plan steps still remain. You are NOT done until the "
        + "checklist is drained. If a remaining step needs a decision only the operator can "
        + "make, use ask_human and stop rather than guessing.\n\n"
        + LedgerNudgeSection(ledger)
        + WorkingStateSection.Build(decisions, verification, changedPaths, stagedRegistries)
        + "Original task:\n" + originalUserPrompt;

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
                + "complete, verify (build + tests) and emit your verdict. Add a step only for "
                + "work you have NOT done yet and that the checklist does not cover — a step "
                + "that would only re-read or re-confirm evidence you already recorded is not "
                + "remaining work, it means you are done: write the verdict.\n"
                + "</system-reminder>";
        return "<system-reminder>\n"
            + "The update_progress tool has not been used recently. If you completed steps, mark "
            + "them done; flip the step you are working on to in_progress. If the plan has "
            + "evolved, restructure the checklist (add, reword, or remove steps — full-state "
            + "replace) so it reflects what you are ACTUALLY doing. Current recorded state:\n"
            + ProgressLedgerRenderer.Render(ledger) + "\n"
            + "If this is still accurate and you are mid-step, ignore this reminder.\n"
            + "</system-reminder>";
    }
}
