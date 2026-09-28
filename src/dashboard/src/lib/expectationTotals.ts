import type { CriterionCounts } from "@/lib/expectationsApi";

// The Criteria met card and the criteria panel phrase the same counts; the phrasing
// lives here once so the two cannot drift apart.

/** A share as a whole percentage, or a dash where there is no measurement. */
export function percentOrDash(value: number | null): string {
  return value === null ? "—" : `${Math.round(value * 100)}%`;
}

/** "3 met of 4 judged", with the not-applicable criteria named beside it when there are any. */
export function judgedSentence(counts: CriterionCounts): string {
  const base = `${counts.met} met of ${counts.judged} judged`;
  return counts.notApplicable > 0 ? `${base} · ${counts.notApplicable} not applicable` : base;
}

/** Overrules applied and overrules left stale, or nothing when there are none. */
export function overruleSentence(counts: CriterionCounts): string | null {
  const parts = [
    counts.overruled > 0 ? `${counts.overruled} overruled` : null,
    counts.staleOverrules > 0
      ? `${counts.staleOverrules} stale overrule${counts.staleOverrules === 1 ? "" : "s"}`
      : null,
  ].filter((p): p is string => p !== null);
  return parts.length === 0 ? null : parts.join(" · ");
}
