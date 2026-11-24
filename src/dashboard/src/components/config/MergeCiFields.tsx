"use client";

import type { ProjectMerge, StudioEntity } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { NumberField, RefSelect } from "./formFields";

// 2026-11-17-3435d: the CI loop's keys of the merge: block. The cadence and the fix budget act only with on_ci_green and
// are shown then; the evidence bounds and the approval identity apply to every mode. A hidden value stays in the form's
// state, so switching the mode away and back never clears it. Empty is the key's default. 2026-11-23-849e: the poll and the
// pending bound drive the CI re-read of every project now (the holds read the CI), so they show for every mode.
// 2026-11-24-f9c5a: the settle and fix keys act for series_when on_ci_green too (absent is on_ci_green), so they show then.

export function MergeCiFields({ merge, secrets, onChange }: {
  merge: ProjectMerge;
  secrets: StudioEntity[];
  onChange: (patch: Partial<ProjectMerge>) => void;
}) {
  const t = useT();
  const number = (key: keyof ProjectMerge, label: string, placeholder: string, help: string) => (
    <NumberField key={key} label={label} value={(merge[key] as number | null | undefined) ?? undefined} placeholder={placeholder}
      help={help} testId={`form-field-merge-${key}`} onChange={(v) => onChange({ [key]: v ?? null })} />
  );
  return (
    <>
      {number("ciPollSeconds", t("config.merge.ciPoll.label"), "60", t("config.merge.ciPoll.help"))}
      {number("ciPendingSeconds", t("config.merge.ciPending.label"), "3600", t("config.merge.ciPending.help"))}
      {(merge.when === "on_ci_green" || (merge.seriesWhen ?? "on_ci_green") === "on_ci_green") && (
        <>
          {number("ciSettleSeconds", t("config.merge.ciSettle.label"), "180", t("config.merge.ciSettle.help"))}
          {number("ciFixAttempts", t("config.merge.ciFix.label"), "3", t("config.merge.ciFix.help"))}
        </>
      )}
      {number("ciExcerptChars", t("config.merge.ciExcerpt.label"), "4000", t("config.merge.ciExcerpt.help"))}
      {number("ciFailedChecksRead", t("config.merge.ciFailedChecks.label"), "5", t("config.merge.ciFailedChecks.help"))}
      <RefSelect label={t("config.merge.approvalAuth.label")} value={merge.approvalAuth ?? ""} options={secrets}
        placeholder={t("config.merge.approvalAuth.placeholder")}
        testId="form-field-merge-approvalAuth" onChange={(v) => onChange({ approvalAuth: v || null })} />
    </>
  );
}
