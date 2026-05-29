"use client";

import type { SubAgentFileWrittenEvent } from "@/types/hub-events";

// p0173f: typed renderer for SubAgentFileWrittenEvent. Path + byte
// count surface the operator-readable identifiers; file content stays
// off the wire (same boundary as the L3 tool-result events).
export function SubAgentFileWrittenRow({ event }: { event: SubAgentFileWrittenEvent }) {
  return (
    <div
      data-testid="sub-agent-file-written-row"
      className="flex items-start gap-2 border-b border-stone-100 py-1 text-xs"
    >
      <span className="mt-0.5 text-stone-400">✏</span>
      <span data-testid="sub-agent-file-path" className="flex-1 truncate text-stone-700">
        {event.path}
      </span>
      <span data-testid="sub-agent-file-bytes" className="text-stone-500">
        {event.bytes} B
      </span>
    </div>
  );
}
