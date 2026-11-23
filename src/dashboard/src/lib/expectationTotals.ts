import type { CriterionCounts } from "@/lib/expectationsApi";
import type { T } from "@/lib/i18n/translate";

// The Criteria met card and the criteria panel phrase the same counts; the phrasing
// lives here once so the two cannot drift apart. 2026-11-23-5669e: in the chosen language.

/** A share as a whole percentage, or a dash where there is no measurement. */
export function percentOrDash(value: number | null): string {
  return value === null ? "—" : `${Math.round(value * 100)}%`;
}

/** "3 met of 4 judged", with the not-applicable criteria named beside it when there are any. */
export function judgedSentence(counts: CriterionCounts, t: T): string {
  const base = t("system.criteria.judged", { met: counts.met, judged: counts.judged });
  return counts.notApplicable > 0
    ? `${base} · ${t("system.criteria.notApplicable", { count: counts.notApplicable })}`
    : base;
}

/** Overrules applied and overrules left stale, or nothing when there are none. */
export function overruleSentence(counts: CriterionCounts, t: T): string | null {
  const parts = [
    counts.overruled > 0 ? t("system.criteria.overruledCount", { count: counts.overruled }) : null,
    counts.staleOverrules > 0 ? t("system.criteria.staleOverrules", { count: counts.staleOverrules }) : null,
  ].filter((p): p is string => p !== null);
  return parts.length === 0 ? null : parts.join(" · ");
}
