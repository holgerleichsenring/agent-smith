"use client";

import type {
  SpecDialogActivityPush,
  SpecDialogReadingPush,
  SpecDialogReadingState,
} from "@/types/spec-dialog";
import { DialogActivityLines } from "./DialogActivityLines";
import { DialogElapsed } from "./DialogElapsed";
import { DialogMessage } from "./DialogTranscript";
import { stepCount } from "./turnSteps";

// 2026-09-15-cb3e, found in use: a design turn materialises the scope's repositories and
// reads them — a long silence is normal, and a page that shows nothing reads as broken.
// 2026-09-17-c7aec: the line names what the turn opened, one line per repository at its
// latest state. 2026-09-17-c7aed: it is the agent's turn in the exchange, and it stated no
// duration, because nothing measured one — RETIRED by 2026-09-18-2f8b, which keeps the turn's
// start instant, so the line states the seconds and the step count below.
// 2026-09-17-042ee: and under those, what the turn DID with them — each tool it called, each
// model call that returned, the review of its own proposal and each re-prompt. Newest last,
// so the eye stays at the bottom where the next line arrives; the older ones fold away,
// because the step the turn is on now is the one being waited for.
// 2026-09-17-042ef: the disclosure over the folded steps is the studio's field label.
// 2026-09-18-2f8b: and it says how MANY steps and how long it has been. The spinning ring is
// behind a motion variant, so a reader who asked for less motion was left with a static ring
// and no sign of life at all; the convention is right and what was missing is a second cue.
// Both render unconditionally.

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

export function DialogWorking({
  readings,
  activity,
  since,
}: {
  readings: SpecDialogReadingPush[];
  activity: SpecDialogActivityPush[];
  /** The moment on this browser's clock to count up from; null while nothing measured one. */
  since: number | null;
}) {
  const opening = readings.some((reading) => reading.state === "opening");
  // What the TURN has taken, not what this page still holds: the kept list is bounded, so a
  // count of it would freeze past that bound and one of the two liveness cues would die.
  const taken = stepCount(activity);
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
          <span data-testid="dialog-working-pulse" className="font-mono dsh-mono text-mute">
            {since !== null && (
              <>
                <DialogElapsed since={since} />
                {" · "}
              </>
            )}
            {taken} {taken === 1 ? "step" : "steps"}
          </span>
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
            <summary className="fl cursor-pointer">
              {folded.length} earlier {folded.length === 1 ? "step" : "steps"}
            </summary>
            <DialogActivityLines steps={folded} testId="dialog-activity-earlier" />
          </details>
        )}
        {latest.length > 0 && <DialogActivityLines steps={latest} testId="dialog-activity" />}
      </div>
    </DialogMessage>
  );
}
