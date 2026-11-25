"use client";

import type { ProjectMerge } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { NumberField } from "./formFields";

// 2026-11-17-3435d: the CI loop's keys of the merge: block. The evidence bounds apply to every review (2026-11-25-9992h: no
// approval identity — nothing approves). Empty is the key's default. 2026-11-23-849e: the poll and the pending bound drive
// the CI re-read of every project (the holds read the CI). 2026-11-25-9992i: the merge block keeps only these keys and each
// acts under every review — the settle bound also gates the requeue of builds that never started — so all show.

export function MergeCiFields({ merge, onChange }: {
  merge: ProjectMerge;
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
      {number("ciSettleSeconds", t("config.merge.ciSettle.label"), "180", t("config.merge.ciSettle.help"))}
      {/* 2026-11-24-6811c: the fix budget also bounds a base refresh's repair passes. */}
      {number("ciFixAttempts", t("config.merge.ciFix.label"), "3", t("config.merge.ciFix.help"))}
      {number("ciExcerptChars", t("config.merge.ciExcerpt.label"), "4000", t("config.merge.ciExcerpt.help"))}
      {number("ciFailedChecksRead", t("config.merge.ciFailedChecks.label"), "5", t("config.merge.ciFailedChecks.help"))}
    </>
  );
}
