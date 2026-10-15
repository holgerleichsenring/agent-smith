import type { AttemptCause, TicketAttemptMark, TicketPullRequest, TicketSummary } from "@/types/tickets";

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

/** 2026-10-14-dbcen: a filed ticket with no run on record — no attempt and no newest run. */
export function isNeverRun(t: TicketSummary): boolean {
  return t.attemptCount === 0 && !t.newestRunId;
}

/** The ticket's name on the row: the tracker's key where one is known, else the raw id. */
export function ticketLabel(t: TicketSummary): string {
  return t.ticketKey ?? `#${t.ticketId}`;
}

const PLATFORM_WORDS: Record<string, string> = { github: "GitHub", gitlab: "GitLab", jira: "Jira", azuredevops: "Azure DevOps" };

/** The tracker's name as a person reads it — "GitHub", "Azure DevOps"; "the tracker" when none is recorded. */
export function platformWords(platform: string | null): string {
  return platform ? PLATFORM_WORDS[platform.toLowerCase()] ?? platform : "the tracker";
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
  series: "series, started automatically",
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

/** The order the pull requests' fate is told in; unsupported and unread say nothing about it. */
const FATE_ORDER: ReadonlyArray<readonly [string, string]> = [
  ["merged", "merged"],
  ["open", "open"],
  ["closed", "closed unmerged"],
];

/** How the pull requests ended so far — "1 PR merged, 1 open"; null when none has a known state. */
export function pullRequestFate(pullRequests: TicketPullRequest[]): string | null {
  const parts = FATE_ORDER.map(([state, words]) => [pullRequests.filter((p) => p.state === state).length, words] as const)
    .filter(([n]) => n > 0);
  if (parts.length === 0) return null;
  const [[first, firstWords], ...rest] = parts;
  const head = `${first} ${first === 1 ? "PR" : "PRs"} ${firstWords}`;
  return [head, ...rest.map(([n, words]) => `${n} ${words}`)].join(", ");
}
