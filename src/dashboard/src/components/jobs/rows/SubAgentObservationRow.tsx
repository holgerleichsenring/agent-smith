"use client";

import type { SubAgentObservationEvent } from "@/types/hub-events";

// p0173f: typed renderer for one SubAgentObservationEvent. The text
// field is rendered as the operator-readable body — never re-serialised
// as a JSON blob.
export function SubAgentObservationRow({ event }: { event: SubAgentObservationEvent }) {
  return (
    <div
      data-testid={`sub-agent-observation-row`}
      className="flex items-start gap-2 border-b border-stone-100 py-1 text-xs"
    >
      <span className="mt-0.5 text-stone-400">·</span>
      <span data-testid="sub-agent-observation-text" className="flex-1 text-stone-700">
        {event.text}
      </span>
    </div>
  );
}
