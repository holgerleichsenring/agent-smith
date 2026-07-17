"use client";

import { useMemo, useRef } from "react";
import type { RunEvent } from "@/types/hub-events";
import type { ExecutionNodeProps } from "@/components/execution/ExecutionNode";
import { StoryBar } from "./StoryBar";
import { VerifySummary } from "./VerifySummary";
import { mapStepsToBeats, buildVerifyView, type Beat } from "./beatMapping";

// p0344: reframes the run as a STORY over the mature master/detail trace. The
// storybar (5 beats) sits on top; the Verify beat's acceptance contract renders
// directly below it. The existing NavRail/DetailPane trace stays untouched
// below this — progressive disclosure, one click away.
//
// TODO(p0344 follow-up): wire real ProgressLedger once exposed on RunSnapshot —
// the beats derive from the execution steps/events we have today, not the
// durable p0341 ledger (done/now/next rows, segmented progress).

interface RunStoryProps {
  nodes: ExecutionNodeProps[];
  events: RunEvent[];
  /** Ask the page to select/scroll a trace node in the rail (a beat's anchor). */
  onSelectStep?: (nodeId: string) => void;
}

export function RunStory({ nodes, events, onSelectStep }: RunStoryProps) {
  const beats = useMemo(() => mapStepsToBeats(nodes), [nodes]);
  const verify = useMemo(() => buildVerifyView(events), [events]);
  const verifyRef = useRef<HTMLDivElement>(null);

  const handleBeatClick = (beat: Beat) => {
    if (beat.key === "verify") {
      verifyRef.current?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }
    if (beat.anchorId) onSelectStep?.(beat.anchorId);
  };

  return (
    <section data-testid="run-story" className="mt-5 space-y-4">
      <StoryBar beats={beats} onBeatClick={handleBeatClick} />
      <div ref={verifyRef}>
        <VerifySummary view={verify} />
      </div>
    </section>
  );
}
