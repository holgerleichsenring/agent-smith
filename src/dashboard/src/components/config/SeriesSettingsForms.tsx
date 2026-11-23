"use client";

import type { SeriesSetting, SettingValue, SpecReviewSetting } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { CheckField, NumberField, keep, patcher } from "./formFields";

// 2026-10-13-8762w: the spec_review and series settings singletons, read live per run. The forms hold no copy of
// either block's rule: a value the run would refuse is refused by the server on save, and SettingsStudio shows
// its reason under the Save button.

// 2026-11-19-4176: no step reviews a spec any more; the review's sizing is retired and only the re-ground is left.
export function SpecReviewForm({ value, onChange }: { value: SpecReviewSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <NumberField label={t("config.specReview.reground.label")} value={value.regroundReadBudget}
      onChange={(v) => set({ regroundReadBudget: keep(v, value.regroundReadBudget) })} testId="setting-specreview-reground"
      help={t("config.specReview.reground.help")} />
  );
}

export function SeriesForm({ value, onChange }: { value: SeriesSetting; onChange: (v: SettingValue) => void }) {
  const set = patcher(value, onChange);
  const t = useT();
  return (
    <>
      <NumberField label={t("config.series.maxSpecs.label")} value={value.maxSpecs}
        onChange={(v) => set({ maxSpecs: keep(v, value.maxSpecs) })} testId="setting-series-maxspecs"
        help={t("config.series.maxSpecs.help")} />
      <NumberField label={t("config.series.perAttempt.label")} value={value.specsPerAttempt}
        onChange={(v) => set({ specsPerAttempt: keep(v, value.specsPerAttempt) })} testId="setting-series-perattempt"
        help={t("config.series.perAttempt.help")} />
      <NumberField label={t("config.series.secondsPerSpec.label")} value={value.secondsPerSpec}
        onChange={(v) => set({ secondsPerSpec: keep(v, value.secondsPerSpec) })} testId="setting-series-seconds"
        help={t("config.series.secondsPerSpec.help")} />
      <NumberField label={t("config.series.budget.label")} value={value.budgetUsd}
        onChange={(v) => set({ budgetUsd: keep(v, value.budgetUsd) })} testId="setting-series-budget"
        help={t("config.series.budget.help")} />
      <NumberField label={t("config.series.wallClock.label")} value={value.wallClockHours}
        onChange={(v) => set({ wallClockHours: keep(v, value.wallClockHours) })} testId="setting-series-hours"
        help={t("config.series.wallClock.help")} />
      <CheckField label={t("config.series.continue.label")} value={value.continueAfterWallTime}
        onChange={(v) => set({ continueAfterWallTime: v })} testId="setting-series-continue" />
      <NumberField label={t("config.series.buildNote.label")} value={value.buildNoteMaxChars}
        onChange={(v) => set({ buildNoteMaxChars: keep(v, value.buildNoteMaxChars) })} testId="setting-series-buildnote"
        help={t("config.series.buildNote.help")} />
    </>
  );
}
