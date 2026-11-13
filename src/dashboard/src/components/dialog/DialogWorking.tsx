"use client";

import type {
  SpecDialogActivityPush,
  SpecDialogReadingPush,
  SpecDialogReadingState,
} from "@/types/spec-dialog";
import { condenseSteps } from "./condenseSteps";
import { DialogElapsed } from "./DialogElapsed";
import { DialogMessage } from "./DialogTranscript";

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
// 2026-11-13-3dc8: REVERSES 92e9c's raw live line. A long turn read "648s · 451 steps" over "448 earlier steps, the
// last 47 listed" and three raw lines — a count nobody reads and a fold nobody opens. The line now shows the same
// condensed lines the turn leaves behind, newest last, under a clock in minutes and seconds; the clock ticking is the
// second cue beside the ring, and a new line arriving is the third.

const READING_WORDS: Record<SpecDialogReadingState, string> = {
  opening: "opening",
  ready: "ready",
  failed: "could not be opened",
  reopening: "removed, reopening",
};

const READING_MARK: Record<SpecDialogReadingState, string> = {
  opening: "before:content-['›_'] text-body",
  ready: "before:content-['✓_'] before:text-primary-deep text-body",
  failed: "before:content-['×_'] text-ink",
  reopening: "before:content-['›_'] text-body",
};

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
  const opening = readings.filter(
    (reading) => reading.state === "opening" || reading.state === "reopening",
  );
  // 2026-09-27-481be: the line above the readings answers for WHAT is being read. It said
  // "the repositories it needs" whatever was open, and a bound conversation reads a ticket too.
  const openingWhat =
    opening.length === 0
      ? "Working it out…"
      : opening.every((reading) => reading.kind === "ticket")
        ? "Reading the ticket…"
        : opening.some((reading) => reading.kind === "ticket")
          ? "Reading the ticket and the repositories it needs…"
          : "Opening the repositories it needs…";
  const lines = condenseSteps(activity);
  return (
    <DialogMessage who="agent" testId="dialog-working">
      <div className="flex flex-col gap-1.5 dsh-body text-body">
        <span className="flex items-center gap-2">
          <span
            aria-hidden="true"
            className="inline-block size-3 animate-spin rounded-full border-2 border-current border-t-transparent motion-reduce:animate-none"
          />
          {openingWhat}
          <span data-testid="dialog-working-pulse" className="font-mono dsh-mono text-mute">
            {since !== null && <DialogElapsed since={since} />}
          </span>
        </span>
        {readings.length > 0 && (
          <ul data-testid="dialog-readings" className="flex flex-col gap-0.5 font-mono dsh-mono">
            {readings.map((reading) => (
              <li
                key={`${reading.kind}/${reading.name}`}
                data-testid="dialog-reading"
                data-state={reading.state}
                className={READING_MARK[reading.state]}
              >
                {reading.name}: {READING_WORDS[reading.state]}
              </li>
            ))}
          </ul>
        )}
        {lines.length > 0 && (
          <ul data-testid="dialog-activity" className="flex flex-col gap-0.5 font-mono dsh-mono text-mute">
            {lines.map((line) => (
              <li key={line.key} data-testid="dialog-activity-line" data-kind={line.kind}>
                {line.text}
              </li>
            ))}
          </ul>
        )}
      </div>
    </DialogMessage>
  );
}
