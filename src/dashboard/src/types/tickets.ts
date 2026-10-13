import type { PendingQuestionInfo } from "@/types/hub-events";

// 2026-10-13-9a59p: the ticket list as GET /api/tickets serves it (TicketListView and the records
// under it in AgentSmith.Server/Models, camelCase). Fields a later member of the series added are
// optional: an older server omits them, and the list draws nothing rather than a guess.

/** The server's buckets, exactly as TicketBucket spells them. */
export type TicketServerBucket =
  | "needs_you"
  | "working"
  | "waiting_on_review"
  | "stuck"
  | "idle"
  | "done";

/** The buckets as the URL spells them (?bucket=), in the order the list shows them. */
export const TICKET_BUCKETS = ["needs-you", "working", "waiting-on-review", "stuck", "ended", "done"] as const;
export type TicketBucket = (typeof TICKET_BUCKETS)[number];

/** URL bucket -> the server's word. "ended" is the server's idle: no bucket owns the ticket. */
export const SERVER_BUCKET: Readonly<Record<TicketBucket, TicketServerBucket>> = {
  "needs-you": "needs_you",
  working: "working",
  "waiting-on-review": "waiting_on_review",
  stuck: "stuck",
  ended: "idle",
  done: "done",
};

/** One attempt as the row marks it, oldest first. */
export interface TicketAttemptMark {
  runId: string;
  status: string;
  endedOnInfrastructure: boolean;
  handedBack: boolean;
}

/** One pull request of a ticket, as its review record says. */
export interface TicketPullRequest {
  number: string;
  url: string;
  repo: string;
  /** open | merged | closed | unsupported | unread */
  state: string;
  /** none | approved | changes-requested | changes-requested-untrusted | rejected */
  decision: string;
  by: string | null;
  lastActivityAt: string | null;
  stateAt: string | null;
  note: string | null;
}

export interface AttemptSource {
  kind: string;
  url?: string | null;
  id?: string | null;
  detail?: string | null;
}

/** Why the newest attempt with a record began (AttemptChannels). */
export interface AttemptCause {
  channel: string;
  actor?: string | null;
  at?: string | null;
  source?: AttemptSource | null;
}

/** done | carried | live | asked | failed | handed_back | not_started */
export type TicketSeriesCellState = string;

export interface TicketSeriesStrip {
  /** cut | deriving | handed_back | none */
  derive: string;
  cells: Array<{ specId: string; state: TicketSeriesCellState }>;
}

/** A needs-you question row's question and the run that asks it. */
export interface TicketPendingQuestion {
  runId: string;
  question: PendingQuestionInfo;
}

export interface TicketSummary {
  project: string;
  ticketId: string;
  ticketKey: string | null;
  platform: string | null;
  title: string | null;
  bucket: TicketServerBucket;
  /** question | handback on a needs-you row */
  kind: string | null;
  since: string | null;
  attemptCount: number;
  cost: number;
  newestRunId: string;
  status: string;
  lastOutcome: string | null;
  queuePlace: number | null;
  pendingQuestion: TicketPendingQuestion | null;
  specsDone: number;
  specsTotal: number;
  attempts: TicketAttemptMark[];
  pullRequests?: TicketPullRequest[];
  cause?: AttemptCause | null;
  trackerState?: string | null;
  webUrl?: string | null;
  seriesStrip?: TicketSeriesStrip | null;
  unchangedSinceLastAttempt?: boolean | null;
  checkedAt?: string | null;
  handback?: { text: string; at: string } | null;
}

export interface TicketCounts {
  needsYou: number;
  working: number;
  running: number;
  queued: number;
  idle: number;
  oldestNeedsYouSince: string | null;
  monthCost: number;
  truncated: boolean;
  waitingOnReview: number;
  done: number;
  openPullRequests: number;
  doneThisMonth: { tickets: number; attempts: number };
  lastDone: { project: string; ticketId: string; at: string } | null;
  stuck: number;
}

export interface TicketListView {
  tickets: TicketSummary[];
  nextCursor: string | null;
  counts: TicketCounts;
}
