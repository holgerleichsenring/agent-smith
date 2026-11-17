import type { RunPasses } from "@/types/tickets";
import { money } from "../ticketFormat";

// 2026-10-13-9a59s: an attempt's cost split by 9a59f's passes read: derive, each spec (its passes plus its cost
// outside passes), pr, other and not attributed. 2026-11-17-8d1cc: written as words on the run's cost line; the bars
// on one scale are gone.

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
