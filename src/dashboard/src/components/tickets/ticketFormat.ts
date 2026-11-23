import type { T, TextKey } from "@/lib/i18n/translate";
import type { AttemptCause, TicketAttempt, TicketAttemptMark, TicketDerivation, TicketPullRequest, TicketSummary } from "@/types/tickets";

// 2026-10-13-9a59p: the words a ticket row is written in, from recorded fields only.

/**
 * A compact age, "41m" or "3h" — the mock's .when column; "now" under a minute. With a lookup the unit is the chosen
 * language's ("41 Min."); "now" stays the marker callers word themselves (2026-11-23-5669b).
 */
export function age(iso: string | null | undefined, t?: T): string | null {
  if (!iso) return null;
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return null;
  const minutes = Math.max(0, Math.round((Date.now() - then) / 60000));
  if (minutes < 1) return "now";
  if (minutes < 60) return t ? t("tickets.age.minutes", { n: minutes }) : `${minutes}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return t ? t("tickets.age.hours", { n: hours }) : `${hours}h`;
  const days = Math.round(hours / 24);
  return t ? t("tickets.age.days", { n: days }) : `${days}d`;
}

/** 2026-11-23-5669b: how long ago, as a phrase — "just now", "3h ago"; null where no time is recorded. */
export function ago(t: T, iso: string | null | undefined): string | null {
  const when = age(iso, t);
  if (when === null) return null;
  return when === "now" ? t("tickets.start.justNow") : t("tickets.start.ago", { when });
}

export function money(usd: number): string {
  return `$${usd.toFixed(2)}`;
}

/** 2026-10-26-7000b: what the ticket cost in all, its attempts and its spec derivations; the tab and its counter share it. */
export function ticketCost(attempts: readonly TicketAttempt[], derivations: readonly TicketDerivation[]): number {
  return attempts.reduce((sum, a) => sum + a.cost, 0) + derivations.reduce((sum, d) => sum + d.cost, 0);
}

export function attempts(t: T, n: number): string {
  return t("tickets.attempts", { count: n });
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
export function platformWords(t: T, platform: string | null): string {
  return platform ? PLATFORM_WORDS[platform.toLowerCase()] ?? platform : t("tickets.theTracker");
}

const CHANNEL_KEYS = {
  ticket_comment: "tickets.channel.ticket_comment",
  pull_request: "tickets.channel.pull_request",
  status_move: "tickets.channel.status_move",
  requested: "tickets.channel.requested",
  retrigger: "tickets.channel.retrigger",
  pickup: "tickets.channel.pickup",
  operator: "tickets.channel.operator",
  retry: "tickets.channel.retry",
  overrule: "tickets.channel.overrule",
  series: "tickets.channel.series",
  precondition: "tickets.channel.precondition",
  ci: "tickets.channel.ci", // 2026-11-17-3435c
} as const satisfies Record<string, TextKey>;

export function channelWords(t: T, channel: string): string {
  const key = (CHANNEL_KEYS as Record<string, TextKey>)[channel];
  return key ? t(key) : channel;
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

const OUTCOME_KEYS: Record<string, TextKey> = {
  success: "tickets.lastOutcome.success",
  shortfall: "tickets.lastOutcome.shortfall",
  failed: "tickets.lastOutcome.failed",
  cancelled: "tickets.lastOutcome.cancelled",
};

export function outcomeWords(t: T, outcome: string): string {
  const key = OUTCOME_KEYS[outcome];
  return key ? t(key) : outcome;
}

/** The order the pull requests' fate is told in; unsupported and unread say nothing about it. */
const FATE_ORDER: ReadonlyArray<readonly [string, TextKey]> = [
  ["merged", "tickets.fate.merged"],
  ["open", "tickets.fate.open"],
  ["closed", "tickets.fate.closed"],
];

/** How the pull requests ended so far — "1 PR merged, 1 open"; null when none has a known state. */
export function pullRequestFate(t: T, pullRequests: TicketPullRequest[]): string | null {
  const parts = FATE_ORDER.map(([state, key]) => [pullRequests.filter((p) => p.state === state).length, t(key)] as const)
    .filter(([n]) => n > 0);
  if (parts.length === 0) return null;
  const [[first, firstWords], ...rest] = parts;
  const head = t("tickets.fate.head", { count: first, words: firstWords });
  return [head, ...rest.map(([n, words]) => `${n} ${words}`)].join(", ");
}
