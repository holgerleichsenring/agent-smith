// 2026-11-23-31dcb: the series runs as the server answers them — GET /api/series-runs (the list), /count (the sidebar)
// and /{id} (one series' page). Named series runs to stay apart from a ticket's spec series (types/tickets.ts).

/** Progress of one series: its tickets, the landed ones, the ones whose pull request waits, the held ones and the
 *  ones never started. The rest are at work, so the four need not add up to the total. */
export interface SeriesRunCounts {
  total: number;
  landed: number;
  waiting: number;
  /** 2026-11-23-31dcc: not merged, nothing waiting to merge, the newest attempt stopped short. */
  held: number;
  notStarted: number;
}

export interface SeriesRunRow {
  id: string;
  /** The conversation's subject, else the first ticket's title. */
  name: string;
  project: string;
  /** running · verifying · paused · done · stopped */
  state: string;
  counts: SeriesRunCounts;
  /** The key of the ticket whose pull requests merge next; null when none is ready. */
  nextMerge: string | null;
  /** The master's question still waiting for a person. */
  question: string | null;
  lastActivityAt: string;
}

export interface SeriesRunActiveCount {
  running: number;
}

export interface SeriesRunPullRequest {
  repo: string;
  number: string;
  url: string;
  /** open · merged · closed · unread · unsupported */
  state: string;
  /** none · pending · green · red, read on the pull request's head; null before a CI read. */
  ci: string | null;
  /** A red head's first failed check and its first line. */
  ciFailure: string | null;
  ciLog: string | null;
  /** The branch it targets — the records hold it only once merged, so null for an open pull request today. */
  target: string | null;
}

export interface SeriesRunTicket {
  ticketId: string;
  key: string;
  title: string;
  followUp: boolean;
  /** The longest requires path to it inside the series; 0 without one. */
  depth: number;
  requires: string[];
  repos: string[];
  landed: boolean;
  /** The newest attempt's status; null before any. */
  status: string | null;
  pullRequests: SeriesRunPullRequest[];
}

/** ready: merge this one now · after: the tickets in `after` land first · retarget: its target moves to main first. */
export type SeriesRunMergeWait = "ready" | "after" | "retarget";

export interface SeriesRunMergeStep {
  ticketId: string;
  key: string;
  depth: number;
  wait: SeriesRunMergeWait;
  after: string[];
}

/** 2026-11-23-31dcf: what a requires edge means for the branches, from the per-repo stack a start is decided by.
 *  stacked: cut from the prerequisite's branch (or one carrying it) · merge: met only by a merge · unknown: the
 *  prerequisite has no green attempt, so its repos are not known · landed: the prerequisite merged. */
export type SeriesRunEdgeKind = "stacked" | "merge" | "unknown" | "landed";

/** 2026-11-23-31dcf: one requires edge between two tickets of the series. */
export interface SeriesRunEdge {
  ticketId: string;
  requiredTicketId: string;
  kind: SeriesRunEdgeKind;
}

export interface SeriesRunDetail {
  id: string;
  name: string;
  project: string;
  state: string;
  /** The Work it out conversation's session id. */
  conversation: string;
  startedAt: string;
  finishedAt: string | null;
  counts: SeriesRunCounts;
  question: string | null;
  /** In depth order, then series order. */
  tickets: SeriesRunTicket[];
  mergeOrder: SeriesRunMergeStep[];
  /** 2026-11-23-31dcd: the repos its tickets have pull requests in, in the project's repo order. */
  repos: string[];
  /** 2026-11-23-31dcd: every run of its tickets plus the master's own loops. */
  costUsd: number;
  /** 2026-11-23-31dcd: the conversation's dashboard thread (its dialog id); null when the session is gone. */
  threadId: string | null;
  /** 2026-11-23-31dcd: whether the conversation is open on that thread. */
  conversationOpen: boolean;
  /** 2026-11-23-31dcf: every requires edge between two of its tickets. */
  edges: SeriesRunEdge[];
}
