"use client";

import type {
  SpecDialogActivityPush,
  SpecDialogReadingPush,
  SpecDialogReadingState,
} from "@/types/spec-dialog";
import { DialogMessage } from "./DialogTranscript";

// 2026-09-15-cb3e, found in use: a design turn materialises the scope's repositories and
// reads them — a long silence is normal, and a page that shows nothing reads as broken.
// 2026-09-17-c7aec: the line names what the turn opened, one line per repository at its
// latest state. 2026-09-17-c7aed: it is the agent's turn in the exchange, and it states no
// duration, because nothing measures one.
// 2026-09-17-042ee: and under those, what the turn DID with them — each tool it called, each
// model call that returned, the review of its own proposal and each re-prompt. Newest last,
// so the eye stays at the bottom where the next line arrives; the older ones fold away,
// because the step the turn is on now is the one being waited for.

const READING_WORDS: Record<SpecDialogReadingState, string> = {
  opening: "opening",
  ready: "ready",
  failed: "could not be opened",
};

const READING_MARK: Record<SpecDialogReadingState, string> = {
  opening: "before:content-['›_'] text-body",
  ready: "before:content-['✓_'] before:text-primary-deep text-body",
  failed: "before:content-['×_'] text-ink",
};

/** How many steps stay in the open; everything older folds into the disclosure above them. */
const SHOWN = 3;

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

function ActivityLines({ steps, testId }: { steps: SpecDialogActivityPush[]; testId: string }) {
  return (
    <ul data-testid={testId} className="flex flex-col gap-0.5 font-mono dsh-mono text-mute">
      {steps.map((step, index) => (
        <li key={`${step.at}-${index}`} data-testid="dialog-activity-line" data-kind={step.kind}>
          {activityLine(step)}
        </li>
      ))}
    </ul>
  );
}

export function DialogWorking({
  readings,
  activity,
}: {
  readings: SpecDialogReadingPush[];
  activity: SpecDialogActivityPush[];
}) {
  const opening = readings.some((reading) => reading.state === "opening");
  const folded = activity.slice(0, Math.max(0, activity.length - SHOWN));
  const latest = activity.slice(folded.length);
  return (
    <DialogMessage who="agent" testId="dialog-working">
      <div className="flex flex-col gap-1.5 dsh-body text-body">
        <span className="flex items-center gap-2">
          <span
            aria-hidden="true"
            className="inline-block size-3 animate-spin rounded-full border-2 border-current border-t-transparent motion-reduce:animate-none"
          />
          {opening ? "Opening the repositories it needs…" : "Working it out…"}
        </span>
        {readings.length > 0 && (
          <ul data-testid="dialog-readings" className="flex flex-col gap-0.5 font-mono dsh-mono">
            {readings.map((reading) => (
              <li
                key={reading.repo}
                data-testid="dialog-reading"
                data-state={reading.state}
                className={READING_MARK[reading.state]}
              >
                {reading.repo}: {READING_WORDS[reading.state]}
              </li>
            ))}
          </ul>
        )}
        {folded.length > 0 && (
          <details data-testid="dialog-activity-folded">
            <summary className="cursor-pointer dsh-label eyebrow-uppercase text-mute">
              {folded.length} earlier {folded.length === 1 ? "step" : "steps"}
            </summary>
            <ActivityLines steps={folded} testId="dialog-activity-earlier" />
          </details>
        )}
        {latest.length > 0 && <ActivityLines steps={latest} testId="dialog-activity" />}
      </div>
    </DialogMessage>
  );
}
