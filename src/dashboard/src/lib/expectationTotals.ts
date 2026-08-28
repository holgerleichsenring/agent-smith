import type { OutcomeCounts, ProjectExpectationMetrics } from "@/lib/expectationsApi";

// 2026-08-27-559e: the per-project outcome counts summed into one reading.
// The criteria CARD and the criteria PANEL both need it, and two copies of the
// sum is how one installation ends up with two hit rates.

export function sumOutcomeCounts(projects: ProjectExpectationMetrics[]): OutcomeCounts {
  return projects.reduce<OutcomeCounts>(
    (acc, p) => ({
      total: acc.total + p.counts.total,
      verbatim: acc.verbatim + p.counts.verbatim,
      edited: acc.edited + p.counts.edited,
      rejected: acc.rejected + p.counts.rejected,
      unratified: acc.unratified + p.counts.unratified,
    }),
    { total: 0, verbatim: 0, edited: 0, rejected: 0, unratified: 0 },
  );
}

/** Criteria a human actually ruled on — the denominator of the hit rate. */
export function ratifiedCount(sum: OutcomeCounts): number {
  return sum.total - sum.unratified;
}

/** A rate as a whole percentage, or a dash where there is no measurement. */
export function percentOrDash(value: number | null): string {
  return value === null ? "—" : `${Math.round(value * 100)}%`;
}
