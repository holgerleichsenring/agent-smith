import type { AttemptCause, TicketAttemptMark, TicketSummary } from "@/types/tickets";

// 2026-10-13-9a59p: the words a ticket row is written in, from recorded fields only.

/** A compact age, "41m" or "3h" — the mock's .when column. */
export function age(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return null;
  const minutes = Math.max(0, Math.round((Date.now() - then) / 60000));
  if (minutes < 1) return "now";
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  return hours < 24 ? `${hours}h` : `${Math.round(hours / 24)}d`;
}

export function money(usd: number): string {
  return `$${usd.toFixed(2)}`;
}

export function attempts(n: number): string {
  return n === 1 ? "1 attempt" : `${n} attempts`;
}

/** The ticket's name on the row: the tracker's key where one is known, else the raw id. */
export function ticketLabel(t: TicketSummary): string {
  return t.ticketKey ?? `#${t.ticketId}`;
}

const CHANNEL_WORDS: Record<string, string> = {
  ticket_comment: "ticket comment",
  pull_request: "pull-request review",
  status_move: "status move",
  requested: "request",
  retrigger: "retrigger",
  pickup: "pickup",
  operator: "approval in the conversation",
  retry: "retry",
  overrule: "overruled default",
};

export function channelWords(channel: string): string {
  return CHANNEL_WORDS[channel] ?? channel;
}

/** Why the newest attempt began: the channel, the status it moved into, and who acted. */
export function causeWords(cause: AttemptCause): string {
  const channel = channelWords(cause.channel);
  const detail = cause.source?.detail ? ` to ${cause.source.detail}` : "";
  const actor = cause.actor ? ` by ${cause.actor}` : "";
  return `${channel}${detail}${actor}`;
}

export function failedAttempts(marks: TicketAttemptMark[]): number {
  return marks.filter((m) => m.status === "failed" && !m.endedOnInfrastructure).length;
}

const OUTCOME_WORDS: Record<string, string> = {
  success: "succeeded",
  shortfall: "done with a shortfall",
  failed: "failed",
  cancelled: "cancelled",
};

export function outcomeWords(outcome: string): string {
  return OUTCOME_WORDS[outcome] ?? outcome;
}
