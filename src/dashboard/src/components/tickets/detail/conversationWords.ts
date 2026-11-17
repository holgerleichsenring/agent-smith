import type { TicketConversationEntry } from "@/types/tickets";

// 2026-10-13-9a59r: how one conversation entry is labelled — the filter it falls under, the
// name it shows (the author only where 9a59h recorded one, else its role) and the where-label — from the
// entry's own source, kind and detail. Nothing here is inferred from text.

const ROLE_NAMES: Record<string, string> = { person: "person", agent: "Agent Smith", system: "system", reviewer: "reviewer" };

export function nameOf(entry: TicketConversationEntry): string {
  if (entry.kind === "edited") return "Ticket text changed";
  if (entry.kind === "cut-review") return "Spec cut reviewer";
  if (entry.kind === "default-answer") return "Default answer";
  return entry.author ?? ROLE_NAMES[entry.role] ?? entry.role;
}

export function whereOf(entry: TicketConversationEntry, attemptNumber: (runId: string | null) => number | null): string {
  const detail = (entry.detail ?? {}) as Record<string, unknown>;
  switch (entry.source) {
    case "ticket": {
      if (entry.kind !== "edited") return "ticket comment";
      const n = attemptNumber(entry.attemptRunId);
      return n !== null ? `seen when attempt ${n} read the ticket` : "seen when an attempt read the ticket";
    }
    case "question":
      if (entry.kind === "default-answer") return "answered at the deadline";
      return entry.kind === "answer" ? "answered in agent-smith" : "question";
    case "pr-review": {
      const pr = prNumber(entry.url) ?? "";
      const kind = entry.kind === "command-answer" ? "command answer" : entry.kind;
      return [`PR ${pr ? `#${pr}` : ""}`.trim(), kind, detail.isBot === true ? "bot" : null].filter(Boolean).join(" · ");
    }
    case "spec-cut":
      if (entry.kind === "cut-review") {
        const cut = typeof detail.attempt === "number" ? `cut ${detail.attempt}` : "cut";
        return `in the run · ${cut}${typeof detail.deliverable === "string" ? ` ${detail.deliverable}` : ""}`;
      }
      return entry.kind === "hand-back" ? "hand-back" : entry.kind;
    case "conversation":
      return "ticket conversation";
    default:
      return entry.source;
  }
}

/** The number a pull request's url ends in (GitHub, GitLab, Azure DevOps). */
export function prNumber(url: string | null | undefined): string | null {
  const match = url ? /(?:pull|pulls|merge_requests|pullrequest)\/(\d+)/i.exec(url) : null;
  return match ? match[1] : null;
}

/** A person's (or a reviewer's) words are drawn as a quote. */
export function isQuote(entry: TicketConversationEntry): boolean {
  return (entry.role === "person" || entry.role === "reviewer") && entry.kind !== "edited";
}

const DECISION_PILLS: Record<string, [string, string]> = {
  approved: ["ok", "approved"],
  "approved-with-suggestions": ["ok", "approved with suggestions"],
  "changes-requested": ["bad", "changes requested"],
  rejected: ["bad", "rejected"],
};

/** A pull-request decision recorded on the entry, as a pill. */
export function decisionPill(entry: TicketConversationEntry): [string, string] | null {
  const decision = (entry.detail as { decision?: unknown } | null | undefined)?.decision;
  return typeof decision === "string" ? DECISION_PILLS[decision] ?? ["q", decision.replace(/-/g, " ")] : null;
}
