"use client";

import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { splitOnUrls } from "@/lib/prUrls";
import { composeTimeBadge } from "@/lib/runStepRail";
import type { LogStep } from "./runLogSections";

// 2026-11-17-6d9eb: the first line of an open step — its status in words, where its time went, and what it said when
// it ended, with any link in that message clickable. What the graph's step panel showed above its body.

const STATUS_WORDS: Record<string, TextKey> = {
  success: "runs.log.step.done", failed: "runs.log.step.failed", error: "runs.log.step.failed", running: "runs.log.step.running",
  cancelled: "runs.log.step.cancelled", waiting_for_input: "runs.log.step.waiting",
};

export function RunStepHead({ step }: { step: LogStep }) {
  const t = useT();
  const { row } = step;
  if (row.planned) return <div className="rl-detail-head rl-dim" data-testid="step-head-planned">{t("runs.log.step.notReached")}</div>;
  const word = STATUS_WORDS[row.status ?? ""];
  const parts = [word ? t(word) : (row.status ?? "").replace(/_/g, " ")];
  const time = composeTimeBadge(row);
  if (time) parts.push(time);
  if (row.sandboxCommands) parts.push(t("runs.log.step.sandboxCommands", { count: row.sandboxCommands }));
  if (row.subAgents) parts.push(t("runs.log.step.subAgents", { count: row.subAgents }));
  return (
    <div className="rl-detail-head" data-testid="step-head">
      <div className={`rl-detail-status ${step.state}`}>{parts.filter(Boolean).join(" · ")}</div>
      {row.resultMessage && (
        <div className="rl-detail-message" data-testid="step-head-message">
          {splitOnUrls(row.resultMessage).map((part, i) =>
            part.isUrl ? <a key={i} href={part.value} target="_blank" rel="noreferrer">{part.value} ↗</a> : <span key={i}>{part.value}</span>)}
        </div>
      )}
    </div>
  );
}
