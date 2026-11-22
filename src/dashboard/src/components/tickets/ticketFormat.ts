import type { T } from "@/lib/i18n/translate";
import type { AttemptCause, TicketAttempt, TicketAttemptMark, TicketDerivation, TicketPullRequest, TicketSummary } from "@/types/tickets";

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

/** 2026-10-26-7000b: what the ticket cost in all, its attempts and its spec derivations; the tab and its counter share it. */
export function ticketCost(attempts: readonly TicketAttempt[], derivations: readonly TicketDerivation[]): number {
  return attempts.reduce((sum, a) => sum + a.cost, 0) + derivations.reduce((sum, d) => sum + d.cost, 0);
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
  precondition: "restarted once the precondition held",
  ci: "red CI on the pull request", // 2026-11-17-3435c
};

export function channelWords(channel: string): string {
  return CHANNEL_WORDS[channel] ?? channel;
}

/** 2026-11-22-50d7a: the channels with a start phrase of their own in the dictionary. */
const START_CHANNELS = [
  "ticket_comment", "pull_request", "status_move", "requested", "retrigger", "pickup", "operator", "retry", "overrule",
  "series", "precondition", "ci",
] as const;
type StartChannel = (typeof START_CHANNELS)[number];

/** 2026-11-22-50d7a: the channels a person always acts on — one without a recorded actor reads "by someone". */
const PERSON_CHANNELS: ReadonlySet<string> = new Set(["ticket_comment", "pull_request", "status_move", "operator", "retry", "overrule"]);

function isStartChannel(channel: string): channel is StartChannel {
  return (START_CHANNELS as readonly string[]).includes(channel);
}

function startPhrase(t: T, cause: AttemptCause): string {
  const status = cause.source?.detail;
  if (cause.channel === "pickup" && status) return t("tickets.start.pickupInto", { status });
  const start = isStartChannel(cause.channel)
    ? t(`tickets.start.${cause.channel}`)
    : t("tickets.start.other", { channel: cause.channel });
  return status ? t("tickets.start.into", { start, status }) : start;
}

/**
 * How an attempt began, as a person reads it: "restarted by Ada", "started by the series". A person channel with no
 * recorded actor names "someone", never an id; the series' own actor is the series itself.
 */
export function causeWords(t: T, cause: AttemptCause): string {
  const start = startPhrase(t, cause);
  if (cause.channel === "series") return start;
  const actor = cause.actor || (PERSON_CHANNELS.has(cause.channel) ? t("tickets.start.someone") : null);
  return actor ? t("tickets.start.by", { start, actor }) : start;
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
