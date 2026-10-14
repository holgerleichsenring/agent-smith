import type { RunPasses } from "@/types/tickets";
import { money } from "../ticketFormat";

// 2026-10-13-9a59s: the Cost section's bars. Every attempt sits on ONE scale — the larger of the largest run
// cap and the largest attempt cost — so rows compare by eye. A cap is per run (Run.BudgetCapUsd), never a
// ticket cap: a smaller cap is marked on its own row, a cost over its cap runs past its marker, and with no cap
// on any attempt the largest cost is the full width. A bar's parts are 9a59f's passes read: derive, each
// spec (its passes plus its cost outside passes), pr, other and not attributed.

export type CostBucketKind = "derive" | "spec" | "pr" | "other" | "unattributed";

export interface CostBucket {
  kind: CostBucketKind;
  label: string;
  usd: number;
  /** A spec's pass count. */
  passes?: number;
}

/** A spec is named S{ordinal} where its ordinal is known, else by its id. */
export function specLabel(specId: string, ordinals: Readonly<Record<string, number>>): string {
  const ordinal = ordinals[specId];
  return ordinal ? `S${ordinal}` : specId;
}

export function costBuckets(passes: RunPasses | null, ordinals: Readonly<Record<string, number>>): CostBucket[] {
  if (!passes) return [];
  const specs = [...passes.specs]
    .sort((l, r) => (ordinals[l.specId] ?? Infinity) - (ordinals[r.specId] ?? Infinity))
    .map((spec): CostBucket => ({
      kind: "spec",
      label: specLabel(spec.specId, ordinals),
      usd: spec.passes.reduce((sum, p) => sum + p.costUsd, 0) + spec.outsidePasses.costUsd,
      passes: spec.passes.length,
    }));
  const buckets: CostBucket[] = [
    { kind: "derive", label: "derive", usd: passes.derive.costUsd },
    ...specs,
    { kind: "pr", label: "PRs", usd: passes.pr.costUsd },
    { kind: "other", label: "other", usd: passes.other.costUsd },
    { kind: "unattributed", label: "not attributed", usd: passes.notAttributed.costUsd },
  ];
  return buckets.filter((b) => b.usd > 0);
}

/** "derive $0.90 · S3 $1.70 (2 passes) · PRs $0.45 · S1, S2 carried $0" — carried specs cost this attempt nothing. */
export function costParts(buckets: CostBucket[], carried: string[], live: boolean): string {
  const parts = buckets.map((b) => {
    const passes = b.passes && b.passes > 1 ? ` (${b.passes} passes)` : "";
    return `${b.label} ${money(b.usd)}${passes}`;
  });
  if (carried.length > 0) parts.push(`${carried.join(", ")} carried $0`);
  if (live && parts.length > 0) parts.push("so far");
  return parts.join(" · ");
}

export interface CostRowInput {
  cost: number;
  /** The run's cost cap; null on Unknown-tier and older runs. */
  cap: number | null;
}

export interface CostRowScale {
  /** Percent of the full width the cost fills. */
  costPct: number;
  /** Where this row's own cap sits, when it is smaller than the full width. */
  capPct: number | null;
  over: boolean;
}

export interface CostScale {
  full: number;
  largestCap: number | null;
  /** True when the largest run cap is the full width. */
  capIsFull: boolean;
  rows: CostRowScale[];
  pct: (usd: number) => number;
}

export function costScale(rows: CostRowInput[]): CostScale {
  const caps = rows.map((r) => r.cap).filter((c): c is number => typeof c === "number" && c > 0);
  const largestCap = caps.length > 0 ? Math.max(...caps) : null;
  const largestCost = Math.max(0, ...rows.map((r) => r.cost));
  const full = Math.max(largestCap ?? 0, largestCost);
  const pct = (usd: number) => (full > 0 ? (usd / full) * 100 : 0);
  return {
    full,
    largestCap,
    capIsFull: largestCap !== null && largestCap >= largestCost,
    pct,
    rows: rows.map((r) => {
      const cap = typeof r.cap === "number" && r.cap > 0 ? r.cap : null;
      return { costPct: pct(r.cost), capPct: cap !== null && cap < full ? pct(cap) : null, over: cap !== null && r.cost > cap };
    }),
  };
}
