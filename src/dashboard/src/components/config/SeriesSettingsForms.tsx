"use client";

import type { SeriesSetting, SettingValue, SpecReviewSetting } from "@/lib/configApi";
import { CheckField, NumberField, keep, patcher } from "./formFields";

// 2026-10-13-8762w: the spec_review and series settings singletons, read live per run. The forms hold no copy of
// either block's rule: a value the run would refuse is refused by the server on save, and SettingsStudio shows
// its reason under the Save button.

// 2026-11-19-4176: no step reviews a spec any more; the review's sizing is retired and only the re-ground is left.
export function SpecReviewForm({ value, onChange }: { value: SpecReviewSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <NumberField label="Re-ground read budget" value={value.regroundReadBudget}
      onChange={(v) => set({ regroundReadBudget: keep(v, value.regroundReadBudget) })} testId="setting-specreview-reground"
      help="file reads that re-check the citations of the spec a run enters; past it a citation is left unchecked" />
  );
}

export function SeriesForm({ value, onChange }: { value: SeriesSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <>
      <NumberField label="Max specs" value={value.maxSpecs}
        onChange={(v) => set({ maxSpecs: keep(v, value.maxSpecs) })} testId="setting-series-maxspecs"
        help="the most specs one ticket's series holds" />
      <NumberField label="Specs per attempt" value={value.specsPerAttempt}
        onChange={(v) => set({ specsPerAttempt: keep(v, value.specsPerAttempt) })} testId="setting-series-perattempt"
        help="the most specs one attempt runs; fewer when the measured spec block does not fit the wall clock left" />
      <NumberField label="Seconds per spec" value={value.secondsPerSpec}
        onChange={(v) => set({ secondsPerSpec: keep(v, value.secondsPerSpec) })} testId="setting-series-seconds"
        help="the whole spec block — build, review, verify; 0 = not measured" />
      <NumberField label="Budget (USD)" value={value.budgetUsd}
        onChange={(v) => set({ budgetUsd: keep(v, value.budgetUsd) })} testId="setting-series-budget"
        help="spend of one chain of attempts before the next is held; 0 = off, no attempt starts by itself" />
      <NumberField label="Wall clock (hours of run time)" value={value.wallClockHours}
        onChange={(v) => set({ wallClockHours: keep(v, value.wallClockHours) })} testId="setting-series-hours"
        help="summed run time of one chain of attempts; time held between attempts does not count" />
      <CheckField label="Continue after a wall-time cancel" value={value.continueAfterWallTime}
        onChange={(v) => set({ continueAfterWallTime: v })} testId="setting-series-continue" />
      <NumberField label="Build note characters" value={value.buildNoteMaxChars}
        onChange={(v) => set({ buildNoteMaxChars: keep(v, value.buildNoteMaxChars) })} testId="setting-series-buildnote"
        help="earlier specs' build notes the next spec is shown; past it the oldest are dropped and named" />
    </>
  );
}
