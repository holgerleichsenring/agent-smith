"use client";

import { useMemo, useRef } from "react";
import type { RunEvent, RunSnapshot } from "@/types/hub-events";
import { StoryBar, type BeatKey } from "./StoryBar";
import { LedgerPanel } from "./LedgerPanel";
import { VerifySummary } from "./VerifySummary";
import { buildVerifyFallback } from "./verifyFallback";

// p0344b: the run as a STORY over the mature master/detail trace. Every panel
// renders REAL data from the snapshot: the storybar from the SERVER-computed
// beats (a run without beats shows no storybar at all — old rows, honest, no
// guessing), the Building beat's persisted p0341 progress ledger, and the
// Verify beat's persisted per-criterion acceptance dispositions (event-derived
// fallback only for runs that predate the field).

interface RunStoryProps {
  snapshot: RunSnapshot | null;
  events: RunEvent[];
}

export function RunStory({ snapshot, events }: RunStoryProps) {
  const fallback = useMemo(() => buildVerifyFallback(events), [events]);
  const ledgerRef = useRef<HTMLDivElement>(null);
  const verifyRef = useRef<HTMLDivElement>(null);

  const beats = snapshot?.beats ?? null;
  const ledger = snapshot?.progressLedger ?? null;
  const hasLedger = !!ledger && ledger.length > 0;

  const handleBeatClick = (beat: BeatKey) => {
    if (beat === "verify") {
      verifyRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
    } else if (beat === "building" && hasLedger) {
      ledgerRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
    }
  };

  // p0343b: the active beat's caption carries the run's REAL step progress.
  const activeCaption =
    snapshot && snapshot.totalSteps > 0
      ? `${snapshot.stepIndex} of ${snapshot.totalSteps}`
      : null;

  return (
    <section data-testid="run-story" className="mt-5 space-y-4">
      {beats && <StoryBar beats={beats} activeCaption={activeCaption} onBeatClick={handleBeatClick} />}
      {hasLedger && (
        <div ref={ledgerRef}>
          <LedgerPanel entries={ledger} />
        </div>
      )}
      <div ref={verifyRef}>
        <VerifySummary acceptance={snapshot?.acceptance ?? null} fallback={fallback} />
      </div>
    </section>
  );
}
