import { ticket } from "../../__tests__/ticketFixtures";
import type { RunSnapshot } from "@/types/hub-events";
import type {
  RunCompute,
  RunComputeSegment,
  RunComputeSpawn,
  RunPass,
  RunPasses,
  TicketAttempt,
  TicketConversation,
  TicketConversationEntry,
  TicketDetail,
  TicketReviewRound,
  TicketReviews,
  TicketSeries,
  TicketSeriesSpec,
} from "@/types/tickets";

// 2026-10-13-9a59q: ticket-page test data in the server's JSON shape.

export function attempt(over: Partial<TicketAttempt> = {}): TicketAttempt {
  return {
    number: 1, runId: "r-1", status: "success", startedAt: "2026-09-04T14:00:00Z", finishedAt: "2026-09-04T14:30:00Z",
    cost: 3.05, endedOnInfrastructure: false, handedBack: false, pullRequests: [], executedSpecs: [],
    pendingQuestion: null, cause: { channel: "ticket_comment", actor: "m.reviewer", at: "2026-09-04T13:59:00Z" },
    revision: { number: 1, cause: "initial derivation", seriesId: "set", carriedIds: [], approvalAt: null },
    ...over,
  };
}

export function spec(over: Partial<TicketSeriesSpec> = {}): TicketSeriesSpec {
  return {
    specId: "s1", ordinal: 1, fileStem: "s1-stem", goal: "Port the publishers", repos: ["portal-api"],
    steps: [{ id: "1", action: "Swap the bus", target: null }, { id: "2", action: "Port publishers", target: null }],
    yaml: "spec: s1", state: "done", executedBy: "r-1", executedByAttempt: 1,
    done: [{ text: "Builds", verdict: "met", note: null, citation: null, overrule: null }],
    unmeasured: [], worked: [],
    ...over,
  };
}

export function series(runId: string, specs: TicketSeriesSpec[], over: Partial<TicketSeries> = {}): TicketSeries {
  return {
    project: "shop", ticketId: "101", runId, attempt: 1, at: "2026-09-04T14:05:00Z", setKey: "set",
    handback: null, gate: { status: "passed", specCount: specs.length, runId }, specs, revisions: [], recorded: true,
    ...over,
  };
}

export function pass(over: Partial<RunPass> = {}): RunPass {
  return {
    ordinal: 1, passInSpec: 1, passInStep: 0, openedBecause: "first", endedBecause: "nothing-left", reengageReason: null,
    verdictEmitted: true, criteriaMet: 1, criteriaTotal: 1, criteria: [{ criterion: "Builds", status: "met", evidence: "test" }],
    gateLine: null, actionablePending: 0, unbackedDone: 0, endedAt: "2026-09-04T14:20:00Z", stepsMoved: [],
    costUsd: 1.2, calls: 4,
    ...over,
  };
}

export function passes(runId: string, specs: Record<string, RunPass[]>, derive = 0.9): RunPasses {
  return {
    runId,
    specs: Object.entries(specs).map(([specId, list]) => ({ specId, passes: list, outsidePasses: { costUsd: 0, calls: 0 } })),
    derive: { costUsd: derive, calls: derive > 0 ? 3 : 0 },
    pr: { costUsd: 0, calls: 0 }, other: { costUsd: 0, calls: 0 }, notAttributed: { costUsd: 0, calls: 0 },
  };
}

export function entry(over: Partial<TicketConversationEntry> = {}): TicketConversationEntry {
  return {
    id: "e-1", sourceId: null, prKey: null, source: "question", kind: "question", role: "agent", author: null,
    isOurs: false, at: "2026-09-04T14:10:00Z", attemptRunId: "r-1", group: null, text: "Keep the outbox?",
    ...over,
  };
}

export function detail(attempts: TicketAttempt[], over: Partial<TicketDetail> = {}): TicketDetail {
  const newest = attempts[attempts.length - 1];
  return {
    summary: ticket({ newestRunId: newest?.runId ?? "r-1", attemptCount: attempts.length, status: newest?.status ?? "success" }),
    branch: "agent-smith/GH-101", attempts, otherRuns: [],
    ...over,
  };
}

export function conversation(entries: TicketConversationEntry[]): TicketConversation {
  return {
    project: "shop", ticketId: "101", entries, attempts: [], sessionId: null, openDialogId: null,
    proposal: null, transcriptHidden: false, threadUnavailable: false,
  };
}

// 2026-10-13-9a59s: the run detail fields the Attempts and Cost tabs read, and the compute record.

export function runDetail(runId: string, over: Partial<RunSnapshot> = {}): RunSnapshot {
  return { runId, llmCalls: 12, budgetCapUsd: 15, reservedGiMinutes: 120, costUsd: 1, ...over } as RunSnapshot;
}

export function segment(over: Partial<RunComputeSegment> = {}): RunComputeSegment {
  return {
    startedAt: "2026-09-04T14:00:00Z", endedAt: "2026-09-04T14:30:00Z", endStatus: "success", modelMs: 600000, park: null,
    ...over,
  };
}

export function spawn(over: Partial<RunComputeSpawn> = {}): RunComputeSpawn {
  return {
    repo: "portal-api", spawnedAt: "2026-09-04T14:01:00Z", disposedAt: "2026-09-04T14:30:00Z", memory: "4Gi",
    endReason: "released-at-end", exitCode: null, oomKilled: null,
    ...over,
  };
}

export function compute(runId: string, over: Partial<RunCompute> = {}): RunCompute {
  return {
    runId, spawns: [spawn()], segments: [segment()], unattributedCalls: 0,
    uncounted: [{ kind: "browser", label: "browser sandboxes" }],
    ...over,
  };
}

// 2026-10-13-8762l: the review rounds read — no attempt and no approval record by default.
export function reviews(over: Partial<TicketReviews> = {}): TicketReviews {
  return { project: "shop", ticketId: "101", attempts: [], conversation: null, ...over };
}

export function reviewRound(over: Partial<TicketReviewRound> = {}): TicketReviewRound {
  return {
    scope: "spec", specId: "s1", round: 1, verdict: "build", reviewers: [], cuts: [], findings: [], residues: [],
    revisionNumber: null, at: "2026-09-04T14:00:00Z", ...over,
  };
}
