"use client";

import type { SpecDialogActivityPush } from "@/types/spec-dialog";

// 2026-09-17-042ee: what a running design turn DID, one line per step — each tool it called,
// each model call that returned, the review of its own proposal and each re-prompt.
// 2026-09-18-2f8b: a line is keyed by the turn it belongs to and its sequence. The list is
// sorted by that sequence, so a step folded in out of order shifts every array index below it
// and an index key would move React's rows under the reader's eyes.

/** One step in words. A tool says what it touched where it has a summary; the two states of
 *  the turn say what the turn is doing, and carry nothing of their own. */
function activityLine(step: SpecDialogActivityPush): string {
  switch (step.kind) {
    case "tool":
      return [step.name, step.detail].filter(Boolean).join(" ");
    case "model":
      return step.detail ? `thinking — ${step.detail}` : "thinking";
    case "reviewing":
      return "reviewing its own proposal against the code";
    case "revising":
      return "revising its answer";
  }
}

export function DialogActivityLines({
  steps,
  testId,
}: {
  steps: SpecDialogActivityPush[];
  testId: string;
}) {
  return (
    <ul data-testid={testId} className="flex flex-col gap-0.5 font-mono dsh-mono text-mute">
      {steps.map((step) => (
        <li
          key={`${step.turnStartedAt}-${step.seq}`}
          data-testid="dialog-activity-line"
          data-kind={step.kind}
        >
          {activityLine(step)}
        </li>
      ))}
    </ul>
  );
}
