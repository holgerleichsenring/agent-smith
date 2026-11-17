"use client";

import type { ToolCallEvent } from "@/types/hub-events";

interface Props {
  event: ToolCallEvent;
}

export function ToolCallRow({ event }: Props) {
  return (
    <div className="flex flex-col gap-0.5 text-xs text-app-text-soft" data-testid="tool-call-row">
      <div className="flex items-center gap-2">
        <span className="text-app-mute">→</span>
        {/* p0222: action verb + target — never a bare "unknown" row. */}
        <span className="font-mono text-app-needs" data-testid="tool-call-verb">{event.tool}</span>
        {event.summary ? (
          <span className="truncate font-mono text-app-text-soft" title={event.summary} data-testid="tool-call-target">
            {event.summary}
          </span>
        ) : (
          <span className="text-app-mute">({event.argsLength}B args)</span>
        )}
        <MetadataTooltip />
      </div>
      {/* p0222: the agent's one-sentence intent for this turn, when narrated. */}
      {event.intent && (
        <span className="pl-5 italic text-app-mute" data-testid="tool-call-intent">
          {event.intent}
        </span>
      )}
    </div>
  );
}

export function MetadataTooltip() {
  return (
    <span
      className="cursor-help text-app-mute"
      title="Tool inputs/outputs are metadata-only — full payloads stay in result.md per p0169e security boundary."
      data-testid="metadata-tooltip"
    >
      ⓘ
    </span>
  );
}
