"use client";

import type { SpecDialogReadingPush, SpecDialogReadingState } from "@/types/spec-dialog";
import { DialogMessage } from "./DialogTranscript";

// 2026-09-15-cb3e, found in use: a design turn materialises the scope's repositories and
// reads them — a long silence is normal, and a page that shows nothing reads as broken.
// 2026-09-17-c7aec: the line names what the turn opened, one line per repository at its
// latest state. 2026-09-17-c7aed: it is the agent's turn in the exchange, and it states no
// duration, because nothing measures one.

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

export function DialogWorking({ readings }: { readings: SpecDialogReadingPush[] }) {
  const opening = readings.some((reading) => reading.state === "opening");
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
      </div>
    </DialogMessage>
  );
}
