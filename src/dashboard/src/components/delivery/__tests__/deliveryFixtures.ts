import type { DeliveryAttempt, DeliveryFigures, DeliveryTicket, DeliveryView } from "@/lib/deliveryApi";

// 2026-10-25-0099c: a Delivery answer as GET /api/delivery serves it, with sample projects only.

export function figures(over: Partial<DeliveryFigures> = {}): DeliveryFigures {
  return {
    ticketsWorked: 2, attempts: 3, merged: 1, mergeRate: 0.5, medianTimeToMergeSeconds: 7200,
    reworkPerTicket: 0.5, spendUsd: 12.4, costPerMergedUsd: 12.4, agentSeconds: 5400, ...over,
  };
}

export function attempt(runId: string, over: Partial<DeliveryAttempt> = {}): DeliveryAttempt {
  return {
    runId, startedAt: "2026-10-24T10:00:00Z", finishedAt: "2026-10-24T11:00:00Z", seconds: 3600,
    outcome: "delivered", cause: "success", rework: false, costUsd: 4.2, specsDone: 2, specsTotal: 2, ...over,
  };
}

export function ticket(ticketId: string, over: Partial<DeliveryTicket> = {}): DeliveryTicket {
  return {
    project: "web-shop", ticketId, title: "Paginate customer search", bucket: "done", mergedAt: "2026-10-24T12:00:00Z",
    attempts: [attempt(`${ticketId}-r1`)], specsDone: 2, specsTotal: 2, specTitles: ["Search pages by cursor", "Client reads pages"],
    repos: ["api"], agentSeconds: 3600, wallSeconds: 7200, costUsd: 4.2, lastActivityAt: "2026-10-24T12:00:00Z", ...over,
  };
}

export function delivery(over: Partial<DeliveryView> = {}): DeliveryView {
  return {
    period: "week", from: "2026-10-18T18:00:00Z", to: "2026-10-25T18:00:00Z", projects: ["billing-service", "web-shop"],
    current: figures(), previous: figures({ ticketsWorked: 1, merged: 0, mergeRate: 0, medianTimeToMergeSeconds: null }),
    slots: Array.from({ length: 7 }, (_, i) => figures({ ticketsWorked: i })),
    endCauses: [{ cause: "success", count: 2 }, { cause: "preflight", count: 1 }],
    byProject: [{ project: "web-shop", tickets: 2, merged: 1, mergeRate: 0.5, costPerMergedUsd: 12.4, medianAttemptSeconds: 3600 }],
    tickets: [
      ticket("GH-101"),
      ticket("GH-102", {
        bucket: "failed", mergedAt: null, title: "Retry invoice export",
        attempts: [attempt("GH-102-r1", { outcome: "failed", cause: "preflight" }), attempt("GH-102-r2", { outcome: "running", cause: null, finishedAt: null, rework: true })],
      }),
    ],
    truncated: false,
    ...over,
  };
}
