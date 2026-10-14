import type { JobsHubClient } from "@/lib/JobsHubClient";
import type { TicketCounts, TicketListView, TicketPullRequest, TicketSummary } from "@/types/tickets";

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
    waitingOnReview: 0,
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
    subscribeOverview: () => Promise.resolve(() => Promise.resolve()),
  };
  return { hub, client: hub as unknown as JobsHubClient };
}
