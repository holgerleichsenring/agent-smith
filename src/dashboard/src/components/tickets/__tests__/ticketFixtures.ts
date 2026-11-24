import { HubConnectionState } from "@microsoft/signalr";
import type { JobsHubClient } from "@/lib/JobsHubClient";
import type { TicketCounts, TicketHold, TicketHoldCase, TicketListView, TicketPullRequest, TicketSummary } from "@/types/tickets";

// 2026-10-13-9a59p: ticket-list test data in the server's JSON shape.

export function ticket(over: Partial<TicketSummary> = {}): TicketSummary {
  return {
    project: "shop",
    ticketId: "101",
    ticketKey: "GH-101",
    platform: "github",
    title: "Fix the login",
    bucket: "working",
    kind: null,
    since: new Date(Date.now() - 6 * 60000).toISOString(),
    attemptCount: 1,
    cost: 1.5,
    newestRunId: "r-101",
    status: "running",
    lastOutcome: null,
    queuePlace: null,
    pendingQuestion: null,
    specsDone: 0,
    specsTotal: 0,
    attempts: [{ runId: "r-101", status: "running", endedOnInfrastructure: false, handedBack: false }],
    pullRequests: [],
    ...over,
  };
}

/** 2026-11-23-31dcg: a hold as the server sends it; the addressee is the case's, as TicketHoldReader reads it. */
const ADDRESSEE: Record<TicketHoldCase, TicketHold["addressee"]> = {
  image_untrusted: "admin", bootstrap_missing: "admin", preflight: "admin",
  question: "decide", question_plan: "plan", question_environment: "admin", handback: "decide", readiness_unknown: "decide", infrastructure: "decide", interrupted: "decide",
  criteria_unmet: "decide", failed: "decide", shortfall: "decide",
  text_edited: "plan", merge_next: "merge", starts_on_merge: "nobody", merge_ready: "merge", ci_failed: "decide", ci_running: "nobody", ci_not_started: "nobody",
  working: "nobody", model_wait: "nobody", queued: "nobody", resuming: "nobody", restarting: "nobody", merge_after: "nobody", retarget: "nobody",
  prerequisite: "nobody",
};

export function hold(holdCase: TicketHoldCase, over: Partial<TicketHold> = {}): TicketHold {
  return { addressee: ADDRESSEE[holdCase], case: holdCase, act: null, since: null, attempt: 1, names: [], detail: null, ...over };
}

export function pr(over: Partial<TicketPullRequest> = {}): TicketPullRequest {
  return {
    number: "809",
    url: "https://example.test/pr/809",
    repo: "server",
    state: "open",
    decision: "none",
    by: null,
    lastActivityAt: null,
    stateAt: null,
    note: null,
    ...over,
  };
}

export function counts(over: Partial<TicketCounts> = {}): TicketCounts {
  return {
    needsYou: 0,
    working: 0,
    running: 0,
    queued: 0,
    idle: 0,
    oldestNeedsYouSince: null,
    monthCost: 0,
    truncated: false,
    done: 0,
    openPullRequests: 0,
    doneThisMonth: { tickets: 0, attempts: 0 },
    lastDone: null,
    stuck: 0,
    ...over,
  };
}

export function list(tickets: TicketSummary[], over: Partial<TicketCounts> = {}, nextCursor: string | null = null): TicketListView {
  return { tickets, nextCursor, counts: counts(over) };
}

type Listener<T> = (value: T) => void;

function subject<T>() {
  const listeners = new Set<Listener<T>>();
  return {
    add(listener: Listener<T>) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    emit(value: T) {
      for (const listener of listeners) listener(value);
    },
  };
}

/** A hub client with the four members the ticket store touches. */
export function fakeHub() {
  const hub = {
    ticketActivityChanged: subject<{ project: string; ticketKey: string }>(),
    runsChanged: subject<string>(),
    connectionState: subject<number>(),
    state: () => HubConnectionState.Disconnected, // 2026-11-23-1e76a: what the hub was at mount
    subscribeOverview: () => Promise.resolve(() => Promise.resolve()),
  };
  return { hub, client: hub as unknown as JobsHubClient };
}
