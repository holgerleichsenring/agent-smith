"use client";

import type { SeriesSetting, SettingValue, SpecReviewSetting } from "@/lib/configApi";
import { CheckField, NumberField, keep, patcher } from "./formFields";

// 2026-10-13-8762w: the spec_review and series settings singletons, read live per run. The forms hold no copy of
// either block's rule: a value the run would refuse is refused by the server on save, and SettingsStudio shows
// its reason under the Save button.

export function SpecReviewForm({ value, onChange }: { value: SpecReviewSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  return (
    <>
      <NumberField label="Rounds per spec" value={value.rounds}
        onChange={(v) => set({ rounds: keep(v, value.rounds) })} testId="setting-specreview-rounds"
        help="adversarial review rounds before a spec is built; 0 = off" />
      <NumberField label="Reviewers per round" value={value.reviewers}
        onChange={(v) => set({ reviewers: keep(v, value.reviewers) })} testId="setting-specreview-reviewers"
        help="fresh reviewers attacking each spec in one round" />
      <NumberField label="Max concurrent reviewers" value={value.maxConcurrent}
        onChange={(v) => set({ maxConcurrent: keep(v, value.maxConcurrent) })} testId="setting-specreview-concurrent" />
      <NumberField label="Budget share (percent)" value={value.budgetSharePercent}
        onChange={(v) => set({ budgetSharePercent: keep(v, value.budgetSharePercent) })} testId="setting-specreview-share"
        help="share of the run's cost cap and wall clock review may use" />
    </>
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
    </>
  );
}
