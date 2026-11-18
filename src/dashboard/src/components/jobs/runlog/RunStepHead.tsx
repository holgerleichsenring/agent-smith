"use client";

import { splitOnUrls } from "@/lib/prUrls";
import { composeTimeBadge } from "@/lib/runStepRail";
import type { LogStep } from "./runLogSections";

// 2026-11-17-6d9eb: the first line of an open step — its status in words, where its time went, and what it said when
// it ended, with any link in that message clickable. What the graph's step panel showed above its body.

const STATUS_WORDS: Record<string, string> = {
  success: "done", failed: "failed", error: "failed", running: "running", cancelled: "cancelled", waiting_for_input: "waiting for an answer",
};

export function RunStepHead({ step }: { step: LogStep }) {
  const { row } = step;
  if (row.planned) return <div className="rl-detail-head rl-dim" data-testid="step-head-planned">The run has not reached this step yet.</div>;
  const parts = [STATUS_WORDS[row.status ?? ""] ?? (row.status ?? "").replace(/_/g, " ")];
  const time = composeTimeBadge(row);
  if (time) parts.push(time);
  if (row.sandboxCommands) parts.push(row.sandboxCommands === 1 ? "1 sandbox command" : `${row.sandboxCommands} sandbox commands`);
  if (row.subAgents) parts.push(row.subAgents === 1 ? "1 sub-agent" : `${row.subAgents} sub-agents`);
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
