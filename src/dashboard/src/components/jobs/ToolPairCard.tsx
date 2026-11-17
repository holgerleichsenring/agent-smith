"use client";

import type { ToolCallEvent, ToolResultEvent } from "@/types/hub-events";
import { MetadataTooltip } from "./ToolCallRow";

interface Props {
  call: ToolCallEvent;
  result: ToolResultEvent;
}

export function ToolPairCard({ call, result }: Props) {
  const okClass = result.ok ? "text-app-proven" : "text-app-stopped";
  return (
    <div className="rounded border border-app-line-strong bg-app-raised px-2 py-1 text-xs" data-testid="tool-pair-card">
      <div className="flex items-center gap-2 text-app-text-soft">
        <span className="text-app-mute">→</span>
        <span className="font-mono text-app-needs">{call.tool}</span>
        {call.summary ? (
          <span className="truncate font-mono text-app-text-soft" title={call.summary}>
            {call.summary}
          </span>
        ) : (
          <span className="text-app-mute">({call.argsLength}B args)</span>
        )}
        <MetadataTooltip />
      </div>
      <div className="mt-0.5 flex items-center gap-2 text-app-mute">
        <span className="text-app-mute">←</span>
        <span className={okClass}>{result.ok ? "ok" : "fail"}</span>
        <span className="text-app-mute">({result.resultLength}B result)</span>
      </div>
    </div>
  );
}
