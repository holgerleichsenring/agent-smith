import { getJson } from "@/lib/apiResponse";
import type { TicketServerBucket } from "@/types/tickets";

// 2026-10-25-0099c: GET /api/delivery (DeliveryView and the records under it in AgentSmith.Server/Models, camelCase)
// — a day, week or month of ticket work in the caller's projects. A figure over nothing is null, never 0.

export type DeliveryPeriod = "day" | "week" | "month";

export type DeliveryOutcome = "running" | "queued" | "waiting" | "delivered" | "shortfall" | "cancelled" | "failed";

export interface DeliveryFigures {
  ticketsWorked: number;
  attempts: number;
  merged: number;
  mergeRate: number | null;
  medianTimeToMergeSeconds: number | null;
  reworkPerTicket: number | null;
  spendUsd: number;
  costPerMergedUsd: number | null;
  agentSeconds: number;
}

export interface DeliveryAttempt {
  runId: string;
  startedAt: string;
  finishedAt: string | null;
  seconds: number;
  outcome: DeliveryOutcome;
  /** The run's EndKind, else its status word; null while it has not ended. */
  cause: string | null;
  rework: boolean;
  costUsd: number;
  specsDone: number;
  specsTotal: number;
}

export interface DeliveryTicket {
  project: string;
  ticketId: string;
  title: string | null;
  /** The bucket the ticket list shows it in; null when the list did not reach it. */
  bucket: TicketServerBucket | null;
  mergedAt: string | null;
  attempts: DeliveryAttempt[];
  specsDone: number;
  specsTotal: number;
  specTitles: string[];
  repos: string[];
  agentSeconds: number;
  wallSeconds: number;
  costUsd: number;
  lastActivityAt: string;
}

export interface DeliveryProject {
  project: string;
  tickets: number;
  merged: number;
  mergeRate: number | null;
  costPerMergedUsd: number | null;
  medianAttemptSeconds: number | null;
}

export interface DeliveryEndCause {
  cause: string;
  count: number;
}

export interface DeliveryView {
  period: DeliveryPeriod;
  from: string;
  to: string;
  /** The projects the caller may read — the chips. */
  projects: string[];
  current: DeliveryFigures;
  previous: DeliveryFigures;
  slots: DeliveryFigures[];
  endCauses: DeliveryEndCause[];
  byProject: DeliveryProject[];
  tickets: DeliveryTicket[];
  truncated: boolean;
}

export function deliveryPath(period: DeliveryPeriod, projects: readonly string[]): string {
  const query = new URLSearchParams({ period });
  for (const project of projects) query.append("project", project);
  return `/api/delivery?${query.toString()}`;
}

export async function fetchDelivery(
  period: DeliveryPeriod, projects: readonly string[], signal?: AbortSignal,
): Promise<DeliveryView> {
  return getJson<DeliveryView>(deliveryPath(period, projects), signal);
}
