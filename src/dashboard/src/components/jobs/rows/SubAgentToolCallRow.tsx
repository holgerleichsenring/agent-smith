"use client";

import type { SubAgentToolCallEvent } from "@/types/hub-events";

// p0173f: typed renderer for SubAgentToolCallEvent. Tool name + the
// producer-curated args summary; never a JSON dump of the raw args.
export function SubAgentToolCallRow({ event }: { event: SubAgentToolCallEvent }) {
  return (
    <div
      data-testid="sub-agent-tool-call-row"
      className="flex items-start gap-2 border-b border-stone-100 py-1 text-xs"
    >
      <span className="mt-0.5 text-stone-400">→</span>
      <span data-testid="sub-agent-tool-name" className="font-mono text-stone-800">
        {event.toolName}
      </span>
      {event.argsSummary ? (
        <span data-testid="sub-agent-tool-args" className="flex-1 truncate text-stone-600">
          {event.argsSummary}
        </span>
      ) : (
        <span className="flex-1 text-stone-400">(no args)</span>
      )}
    </div>
  );
}
