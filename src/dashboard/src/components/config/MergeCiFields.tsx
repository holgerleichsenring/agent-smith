"use client";

import type { ProjectMerge, StudioEntity } from "@/lib/configApi";
import { NumberField, RefSelect } from "./formFields";

// 2026-11-17-3435d: the CI loop's keys of the merge: block. The cadence and the fix budget act only with on_ci_green and
// are shown then; the evidence bounds and the approval identity apply to every mode. A hidden value stays in the form's
// state, so switching the mode away and back never clears it. Empty is the key's default. 2026-11-23-849e: the poll and the
// pending bound drive the CI re-read of every project now (the holds read the CI), so they show for every mode.

export function MergeCiFields({ merge, secrets, onChange }: {
  merge: ProjectMerge;
  secrets: StudioEntity[];
  onChange: (patch: Partial<ProjectMerge>) => void;
}) {
  const number = (key: keyof ProjectMerge, label: string, placeholder: string, help: string) => (
    <NumberField key={key} label={label} value={(merge[key] as number | null | undefined) ?? undefined} placeholder={placeholder}
      help={help} testId={`form-field-merge-${key}`} onChange={(v) => onChange({ [key]: v ?? null })} />
  );
  return (
    <>
      {number("ciPollSeconds", "CI poll seconds", "60", "how often a pending CI of the open pull requests, and a running deploy, is read")}
      {number("ciPendingSeconds", "CI pending seconds", "3600", "after this a head still pending leaves the fast poll, and a deploy stops being followed")}
      {merge.when === "on_ci_green" && (
        <>
          {number("ciSettleSeconds", "CI settle seconds", "180", "how long 'no CI reporting' must last before it merges")}
          {number("ciFixAttempts", "CI fix attempts", "3", "red-CI fix runs in a row before the ticket is handed back")}
        </>
      )}
      {number("ciExcerptChars", "CI excerpt characters", "4000", "evidence kept per failed check: summary, then the log's end")}
      {number("ciFailedChecksRead", "CI failed checks read", "5", "failed checks of a red head whose logs are read")}
      <RefSelect label="approval identity" value={merge.approvalAuth ?? ""} options={secrets} placeholder="— the repo's token —"
        testId="form-field-merge-approvalAuth" onChange={(v) => onChange({ approvalAuth: v || null })} />
    </>
  );
}
