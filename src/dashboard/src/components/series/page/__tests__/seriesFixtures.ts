import { hold } from "@/components/tickets/__tests__/ticketFixtures";
import type { SeriesRunDetail, SeriesRunPullRequest, SeriesRunTicket } from "@/types/seriesRuns";

// 2026-11-23-31dcd: a series detail for the page tests.

export function seriesTicket(over: Partial<SeriesRunTicket> = {}): SeriesRunTicket {
  return {
    ticketId: "48", key: "#48", title: "Approval contracts", followUp: false, depth: 0, requires: [], repos: ["server"],
    landed: false, status: null, pullRequests: [], kind: over.landed ? "merged" : "waiting", state: "", ci: null, working: false, ...over,
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
      seriesTicket({ ticketId: "49", key: "#49", title: "Domain", depth: 1, kind: "pr", ci: "green",
        pullRequests: [{ repo: "server", number: "9935", url: "u", state: "open", ci: "green", ciFailure: null, ciLog: null, target: null }] }),
    ],
    mergeOrder: [], repos: ["server", "worker"], costUsd: 41.2, threadId: "d-thread1", conversationOpen: true, edges: [], workingOn: [], ...over,
  };
}

const pr = (number: string, over: Partial<SeriesRunPullRequest> = {}): SeriesRunPullRequest => ({
  repo: "server", number, url: `https://git.test/server/pull/${number}`, state: "open", ci: "green", ciFailure: null, ciLog: null,
  target: null, ...over,
});

/** 2026-11-23-f562: one ticket of every Overview colour — 48 merged, 50 the merge now, 51 and 55 merges #62 needs, 61 a
 *  plan question, 59 failed, 63 at work, 56 a pull request waiting, 62 waiting on merges. 2026-11-25-9992a: each with the
 *  kind the server computes for it (SeriesTicketState). */
export function overviewDetail(): SeriesRunDetail {
  return seriesDetail({
    tickets: [
      seriesTicket({ ticketId: "48", key: "#48", landed: true, status: "success", pullRequests: [pr("9930", { state: "merged" })], kind: "merged" }),
      seriesTicket({ ticketId: "50", key: "#50", title: "Lifecycle", depth: 1, requires: ["48"], status: "success", runId: "run-50",
        kind: "now", ci: "green", pullRequests: [pr("9937")], hold: hold("merge_next", { next: "#51", names: ["#62"], act: { kind: "merge", target: "sr-1" } }) }),
      seriesTicket({ ticketId: "61", key: "#61", title: "Expiry", depth: 2, requires: ["50"], repos: ["worker"], status: "waiting_for_input",
        runId: "run-61", kind: "question", hold: hold("question_plan", { act: { kind: "conversation", target: "sr-1" } }) }),
      seriesTicket({ ticketId: "59", key: "#59", title: "Admin forms", depth: 2, requires: ["50"], repos: ["client"], status: "failed",
        runId: "run-59", kind: "failed", hold: hold("failed", { act: { kind: "conversation", target: null } }) }),
      seriesTicket({ ticketId: "51", key: "#51", title: "External API", depth: 2, requires: ["50"], status: "success",
        kind: "needed", ci: "green", pullRequests: [pr("9950")], hold: hold("merge_after", { names: ["#50"] }) }),
      seriesTicket({ ticketId: "55", key: "#55", title: "Recipient API", depth: 2, requires: ["50"], status: "success",
        kind: "needed", ci: "green", pullRequests: [pr("9939", { isDraft: true })], hold: hold("merge_after", { names: ["#50"] }) }),
      seriesTicket({ ticketId: "56", key: "#56", title: "Request list", depth: 3, requires: ["55"], repos: ["client"], status: "success",
        kind: "pr", ci: "green", pullRequests: [pr("9980", { repo: "client", url: "https://git.test/client/pull/9980" })], hold: hold("merge_after", { names: ["#55"] }) }),
      seriesTicket({ ticketId: "63", key: "#63", title: "Review dropdown", depth: 3, requires: ["51"], repos: ["client"], status: "running",
        runId: "run-63", kind: "running", working: true, hold: hold("working") }),
      seriesTicket({ ticketId: "62", key: "#62", title: "Review API", depth: 3, requires: ["51", "55"], repos: [], kind: "waiting",
        hold: hold("starts_on_merge", { names: ["#51", "#55"], merges: 3, next: "#50", act: { kind: "series", target: "sr-1" } }) }),
    ],
    mergeOrder: [
      { ticketId: "50", key: "#50", depth: 1, wait: "ready", after: [], neededBy: ["#62"] },
      { ticketId: "51", key: "#51", depth: 2, wait: "after", after: ["#50"], neededBy: ["#62"] },
      { ticketId: "55", key: "#55", depth: 2, wait: "after", after: ["#50"], neededBy: ["#62"] },
      { ticketId: "56", key: "#56", depth: 3, wait: "after", after: ["#55"] },
    ],
    edges: [
      { ticketId: "50", requiredTicketId: "48" }, { ticketId: "61", requiredTicketId: "50" }, { ticketId: "59", requiredTicketId: "50" },
      { ticketId: "51", requiredTicketId: "50" }, { ticketId: "55", requiredTicketId: "50" }, { ticketId: "56", requiredTicketId: "55" },
      { ticketId: "63", requiredTicketId: "51" }, { ticketId: "62", requiredTicketId: "51" }, { ticketId: "62", requiredTicketId: "55" },
    ],
    repos: ["server", "worker", "client"], workingOn: ["#63"],
  });
}
