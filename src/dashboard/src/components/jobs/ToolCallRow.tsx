"use client";

import type { ToolCallEvent } from "@/types/hub-events";

interface Props {
  event: ToolCallEvent;
}

export function ToolCallRow({ event }: Props) {
  return (
    <div className="flex items-center gap-2 text-xs text-stone-300" data-testid="tool-call-row">
      <span className="text-stone-500">→</span>
      <span className="font-mono text-amber-300">{event.tool}</span>
      {event.summary ? (
        <span className="truncate font-mono text-stone-300" title={event.summary}>
          {event.summary}
        </span>
      ) : (
        <span className="text-stone-500">({event.argsLength}B args)</span>
      )}
      <MetadataTooltip />
    </div>
  );
}

export function MetadataTooltip() {
  return (
    <span
      className="cursor-help text-stone-500"
      title="Tool inputs/outputs are metadata-only — full payloads stay in result.md per p0169e security boundary."
      data-testid="metadata-tooltip"
    >
      ⓘ
    </span>
  );
}
