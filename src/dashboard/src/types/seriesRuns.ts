// 2026-11-23-31dcb: the series runs as the server answers them — GET /api/series-runs (the list), /count (the sidebar)
// and /{id} (one series' page). Named series runs to stay apart from a ticket's spec series (types/tickets.ts).

import type { TicketHold } from "@/types/tickets";

/** Progress of one series: its tickets, the landed ones, the ones whose pull request waits, the held ones and the
 *  ones never started. The rest are at work, so the four need not add up to the total. */
export interface SeriesRunCounts {
  total: number;
  landed: number;
  waiting: number;
  /** 2026-11-23-31dcg: the tickets whose hold names someone and is not the next merge. */
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
  /** none · pending · not_started (2026-11-23-0219) · green · red, read on the pull request's head; null before a CI read. */
  ci: string | null;
  /** A red head's first failed check and its first line. */
  ciFailure: string | null;
  ciLog: string | null;
  /** The branch it targets — the records hold it only once merged, so null for an open pull request today. */
  target: string | null;
  /** 2026-11-23-8d16: the newest read saw it still a draft; a person's merge takes it out of draft first. */
  isDraft?: boolean;
  /** 2026-11-24-6811a: the host said last that it conflicts with its base. */
  hasConflicts?: boolean;
  /** 2026-11-24-6811d: the refresh-base run at work on it. */
  refreshRunId?: string | null;
  /** 2026-11-23-0219: the newest merge run's refusal or armed auto-complete on it; null when none said one. */
  mergeSaid?: SeriesMergeSaid | null;
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
  /** 2026-11-23-31dcg: the ticket's hold, as its ticket page and the ticket list read it. */
  hold?: TicketHold | null;
  /** 2026-11-23-f562: the newest attempt's run id — Retry, See why and See run act on it; null before any. */
  runId?: string | null;
}

/** 2026-11-23-0219: what a merge run said about a pull request it did not merge (PullRequestMergeSaid). */
export interface SeriesMergeSaid {
  /** completion_refused · completion_armed */
  status: string;
  reason: string | null;
  at: string;
}

/** ready: merge this one now · after: the tickets in `after` land first · retarget: its target moves to main first. */
export type SeriesRunMergeWait = "ready" | "after" | "retarget";

export interface SeriesRunMergeStep {
  ticketId: string;
  key: string;
  depth: number;
  wait: SeriesRunMergeWait;
  after: string[];
  /** 2026-11-23-8d16: the keys of the tickets that start only once this step merged, from their holds (the server's rule). */
  neededBy?: string[];
}

/** 2026-11-23-31dcf: one requires edge between two tickets of the series (2026-11-23-f562: without a kind). */
export interface SeriesRunEdge {
  ticketId: string;
  requiredTicketId: string;
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
  /** 2026-11-23-0219: the project merges with respect_branch_protection: true, so a Merge while CI waits arms auto-complete. */
  mergeArms?: boolean;
}

/** 2026-11-23-31dce: what a merge from the series page did to one repo's pull request. */
export interface SeriesMergeOutcome {
  repo: string;
  /** merged · armed (the host merges once its rules pass) · refused */
  outcome: "merged" | "armed" | "refused";
  /** The merge run's sentence for it, naming the repo and the host's or guard's reason. */
  text: string;
}

/** 2026-11-23-31dce: the 200 answer of POST /api/series-runs/{id}/tickets/{ticketId}/merge. */
export interface SeriesMergeAnswer {
  ticketId: string;
  pullRequests: SeriesMergeOutcome[];
}

/** 2026-11-23-31dcj: one revision of a series ticket's spec set after its first, as its ticket branch records it. */
export interface SeriesRunChange {
  ticketId: string;
  revision: number;
  /** What the revision names as its cause: an approval in a conversation, a run's revision, a re-cut demand… */
  cause: string;
  at: string;
}

/** 2026-11-23-31dcj: the answer of GET /api/series-runs/{id}/changes — newest first. */
export interface SeriesRunChanges {
  changes: SeriesRunChange[];
  /** The tickets whose branch could not be read, so their changes are missing. */
  unread: { ticketId: string; why: string }[];
}
