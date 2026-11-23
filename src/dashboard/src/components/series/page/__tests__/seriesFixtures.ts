import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";

// 2026-11-23-31dcd: a series detail for the page tests.

export function seriesTicket(over: Partial<SeriesRunTicket> = {}): SeriesRunTicket {
  return {
    ticketId: "48", key: "#48", title: "Approval contracts", followUp: false, depth: 0, requires: [], repos: ["server"],
    landed: false, status: null, pullRequests: [], ...over,
  };
}

export function seriesDetail(over: Partial<SeriesRunDetail> = {}): SeriesRunDetail {
  return {
    id: "sr-1", name: "Approval workflow", project: "shop", state: "running", conversation: "conv-1",
    startedAt: "2026-11-19T10:00:00Z", finishedAt: null,
    counts: { total: 3, landed: 1, waiting: 1, held: 0, notStarted: 1 }, question: null,
    tickets: [
      seriesTicket({ ticketId: "50", key: "#50", title: "Lifecycle", depth: 2 }),
      seriesTicket({ ticketId: "48", key: "#48", landed: true }),
      seriesTicket({ ticketId: "49", key: "#49", title: "Domain", depth: 1,
        pullRequests: [{ repo: "server", number: "9935", url: "u", state: "open", ci: "green", ciFailure: null, ciLog: null, target: null }] }),
    ],
    mergeOrder: [], repos: ["server", "worker"], costUsd: 41.2, threadId: "d-thread1", conversationOpen: true, edges: [], ...over,
  };
}
