import { hasPullRequest } from "@/lib/prStatus";
import type { ProgressLedgerEntry, PullRequestStatus, RunSnapshot } from "@/types/hub-events";
import type { RunSection, SectionState } from "./runSections";

// 2026-11-17-d6fbb: the three sections that hold what the run read and wrote rather than the steps it took — the
// ticket as the run read it, the spec and plan it worked from, and how it ended. They sit around the step sections in
// the log, closed like them; their bodies are the story view's own panels.

export type StorySectionId = "ticket" | "plan" | "outcome";

const section = (id: StorySectionId, title: string, state: SectionState, summary: string): RunSection =>
  ({ id, title, short: title, state, summary, seconds: 0, lines: [] });

export function ticketSection(snapshot: RunSnapshot | null): RunSection {
  const named = [snapshot?.ticketId, snapshot?.ticketTitle].filter(Boolean).join(" · ");
  return section("ticket", "Ticket", named ? "done" : "pending", named || "the ticket as the run read it");
}

export function planSection(ledger: ProgressLedgerEntry[] | null): RunSection {
  if (!ledger || ledger.length === 0) return section("plan", "Spec and plan", "pending", "no plan recorded yet");
  const done = ledger.filter((e) => e.status === "done").length;
  const state: SectionState = done === ledger.length ? "done" : ledger.some((e) => e.status === "in_progress") ? "running" : "pending";
  return section("plan", "Spec and plan", state, `${done} of ${ledger.length} plan steps done`);
}

export function outcomeSection(snapshot: RunSnapshot | null): RunSection {
  const status = (snapshot?.status ?? "").toLowerCase();
  const prs = (snapshot?.pullRequests ?? []).filter((p) => hasPullRequest(p.status as PullRequestStatus) && p.url).length
    || (snapshot?.prUrl ? 1 : 0);
  const delivered = prs === 0 ? "no pull request opened" : prs === 1 ? "1 pull request" : `${prs} pull requests`;
  switch (status) {
    case "success": return section("outcome", "Outcome", "done", delivered);
    case "shortfall": return section("outcome", "Outcome", "warn", `delivered with a shortfall, ${delivered}`);
    case "cancelled": return section("outcome", "Outcome", "pending", "cancelled");
    case "running": case "queued": case "waiting_for_input": case "":
      return section("outcome", "Outcome", "pending", "not finished yet");
    default: return section("outcome", "Outcome", "bad", `stopped, ${delivered}`);
  }
}
