import type { T, TextKey } from "@/lib/i18n/translate";
import type { TicketConversationEntry } from "@/types/tickets";

// 2026-10-13-9a59r: how one conversation entry is labelled — the filter it falls under, the
// name it shows (the author only where 9a59h recorded one, else its role) and the where-label — from the
// entry's own source, kind and detail. Nothing here is inferred from text.

const ROLE_NAMES: Record<string, TextKey> = {
  person: "tickets.conversation.role.person", agent: "tickets.conversation.role.agent", system: "tickets.conversation.role.system",
  reviewer: "tickets.conversation.role.reviewer",
};

export function nameOf(t: T, entry: TicketConversationEntry): string {
  if (entry.kind === "edited") return t("tickets.conversation.name.edited");
  if (entry.kind === "cut-review") return t("tickets.conversation.name.cutReview");
  if (entry.kind === "default-answer") return t("tickets.conversation.name.defaultAnswer");
  return entry.author ?? (ROLE_NAMES[entry.role] ? t(ROLE_NAMES[entry.role]) : entry.role);
}

export function whereOf(t: T, entry: TicketConversationEntry, attemptNumber: (runId: string | null) => number | null): string {
  const detail = (entry.detail ?? {}) as Record<string, unknown>;
  switch (entry.source) {
    case "ticket": {
      if (entry.kind !== "edited") return t("tickets.conversation.where.ticketComment");
      const n = attemptNumber(entry.attemptRunId);
      return n !== null ? t("tickets.conversation.where.seenBy", { n }) : t("tickets.conversation.where.seen");
    }
    case "question":
      if (entry.kind === "default-answer") return t("tickets.conversation.where.atDeadline");
      return entry.kind === "answer" ? t("tickets.conversation.where.answered") : t("tickets.conversation.where.question");
    case "pr-review": {
      const pr = prNumber(entry.url) ?? "";
      const kind = entry.kind === "command-answer" ? t("tickets.conversation.where.commandAnswer") : entry.kind;
      return [`PR ${pr ? `#${pr}` : ""}`.trim(), kind, detail.isBot === true ? t("tickets.conversation.where.bot") : null].filter(Boolean).join(" · ");
    }
    case "spec-cut":
      if (entry.kind === "cut-review") {
        const cut = typeof detail.attempt === "number" ? t("tickets.conversation.where.cutN", { n: detail.attempt }) : t("tickets.conversation.where.cut");
        return t("tickets.conversation.where.inTheRun", { cut: `${cut}${typeof detail.deliverable === "string" ? ` ${detail.deliverable}` : ""}` });
      }
      return entry.kind === "hand-back" ? t("tickets.conversation.where.handBack") : entry.kind;
    case "conversation":
      return t("tickets.conversation.where.conversation");
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

const DECISION_PILLS: Record<string, [string, TextKey]> = {
  approved: ["ok", "tickets.conversation.decision.approved"],
  "approved-with-suggestions": ["ok", "tickets.conversation.decision.approvedWithSuggestions"],
  "changes-requested": ["bad", "tickets.conversation.decision.changesRequested"],
  rejected: ["bad", "tickets.conversation.decision.rejected"],
};

/** A pull-request decision recorded on the entry, as a pill. */
export function decisionPill(t: T, entry: TicketConversationEntry): [string, string] | null {
  const decision = (entry.detail as { decision?: unknown } | null | undefined)?.decision;
  if (typeof decision !== "string") return null;
  const pill = DECISION_PILLS[decision];
  return pill ? [pill[0], t(pill[1])] : ["q", decision.replace(/-/g, " ")];
}
