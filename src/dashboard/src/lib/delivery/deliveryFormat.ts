import type { DeliveryFigures, DeliveryOutcome, DeliveryPeriod } from "@/lib/deliveryApi";

// 2026-10-25-0099c: how the Delivery page writes its numbers and words. Copy follows the approved mock
// (design/mockups/delivery-overview.html); an empty value is "-", never an em dash.

export const NONE = "-";
const MINUS = "−";

export function formatDuration(seconds: number): string {
  if (seconds < 3600) return `${Math.round(seconds / 60)} min`;
  if (seconds < 48 * 3600) return `${(seconds / 3600).toFixed(1)} h`;
  return `${(seconds / 86400).toFixed(1)} d`;
}

export const formatUsd = (value: number) => `$${value.toFixed(2)}`;
export const formatRate = (value: number) => `${Math.round(value * 100)}%`;

export const COMPARED_WITH: Readonly<Record<DeliveryPeriod, string>> = {
  day: "vs yesterday",
  week: "vs last week",
  month: "vs last month",
};

type Kind = "count" | "rate" | "duration" | "money" | "ratio";

export interface Kpi {
  label: string;
  value: (f: DeliveryFigures) => number | null;
  kind: Kind;
  /** Whether a higher value is the better one. */
  higherIsBetter: boolean;
}

export const KPIS: readonly Kpi[] = [
  { label: "Tickets worked", value: (f) => f.ticketsWorked, kind: "count", higherIsBetter: true },
  { label: "Merged", value: (f) => f.merged, kind: "count", higherIsBetter: true },
  { label: "Merge rate", value: (f) => f.mergeRate, kind: "rate", higherIsBetter: true },
  { label: "Median time to merge", value: (f) => f.medianTimeToMergeSeconds, kind: "duration", higherIsBetter: false },
  { label: "Rework rounds per ticket", value: (f) => f.reworkPerTicket, kind: "ratio", higherIsBetter: false },
  { label: "Cost per merged PR", value: (f) => f.costPerMergedUsd, kind: "money", higherIsBetter: false },
  { label: "Spend", value: (f) => f.spendUsd, kind: "money", higherIsBetter: false },
  { label: "Agent hours", value: (f) => f.agentSeconds, kind: "duration", higherIsBetter: false },
];

export function formatValue(kind: Kind, value: number | null): string {
  if (value === null) return NONE;
  switch (kind) {
    case "count": return String(value);
    case "rate": return formatRate(value);
    case "duration": return formatDuration(value);
    case "money": return formatUsd(value);
    case "ratio": return value.toFixed(2);
  }
}

export interface Delta {
  text: string;
  /** True when the change is an improvement, false when it is worse, null when nothing changed. */
  better: boolean | null;
}

/** The change against the previous period; null when either side has no value. */
export function formatDelta(kpi: Kpi, current: number | null, previous: number | null): Delta | null {
  if (current === null || previous === null) return null;
  const diff = current - previous;
  if (Math.abs(diff) < 1e-9) return { text: "no change", better: null };
  const sign = diff > 0 ? "+" : MINUS;
  const size = Math.abs(diff);
  const text = kpi.kind === "rate" ? `${sign}${Math.round(size * 100)} pt` : `${sign}${formatValue(kpi.kind, size)}`;
  return { text, better: diff > 0 === kpi.higherIsBetter };
}

export const OUTCOME_LABEL: Readonly<Record<DeliveryOutcome, string>> = {
  running: "Running",
  queued: "Queued",
  waiting: "Waiting for a person",
  delivered: "Delivered",
  shortfall: "Criteria not met",
  cancelled: "Cancelled",
  failed: "Failed",
};

/** Every cause GET /api/delivery sends: the run end kinds and the status words. */
export const CAUSE_LABEL: Readonly<Record<string, string>> = {
  success: "Delivered",
  shortfall: "Delivered in part",
  "criteria-unmet": "Criteria not met",
  preflight: "Preflight failed (build, deps)",
  "bootstrap-missing": "Repository not initialised",
  "image-untrusted": "Sandbox image not trusted",
  superseded: "Superseded by a newer attempt",
  cancelled: "Cancelled",
  failed: "Failed",
};

/** The colour an end cause is drawn in — the outcome it stands for. */
export const CAUSE_OUTCOME: Readonly<Record<string, DeliveryOutcome>> = {
  success: "delivered",
  shortfall: "shortfall",
  "criteria-unmet": "shortfall",
  superseded: "cancelled",
  cancelled: "cancelled",
};

export const causeLabel = (cause: string) => CAUSE_LABEL[cause] ?? cause;
export const causeOutcome = (cause: string): DeliveryOutcome => CAUSE_OUTCOME[cause] ?? "failed";

/** A UTC instant as the page writes it: "10-25 18:00". */
export const formatAt = (iso: string) => iso.slice(5, 16).replace("T", " ");

const plural = (n: number, word: string) => `${n} ${word}${n === 1 ? "" : "s"}`;

/** What the newest attempt worked on: its specs, the repositories it changed and its first goal. */
export function formatWork(specTitles: readonly string[], repos: readonly string[]): string {
  const parts = [plural(specTitles.length, "spec")];
  if (repos.length > 0) parts.push(plural(repos.length, "repo"));
  if (specTitles.length > 0) parts.push(specTitles[0]);
  return specTitles.length === 0 && repos.length === 0 ? NONE : parts.join(" · ");
}
