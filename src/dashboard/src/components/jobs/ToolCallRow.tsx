"use client";

import type { ToolCallEvent } from "@/types/hub-events";

interface Props {
  event: ToolCallEvent;
}

export function ToolCallRow({ event }: Props) {
  return (
    <div className="flex flex-col gap-0.5 text-xs text-stone-300" data-testid="tool-call-row">
      <div className="flex items-center gap-2">
        <span className="text-stone-500">→</span>
        {/* p0222: action verb + target — never a bare "unknown" row. */}
        <span className="font-mono text-amber-300" data-testid="tool-call-verb">{event.tool}</span>
        {event.summary ? (
          <span className="truncate font-mono text-stone-300" title={event.summary} data-testid="tool-call-target">
            {event.summary}
          </span>
        ) : (
          <span className="text-stone-500">({event.argsLength}B args)</span>
        )}
        <MetadataTooltip />
      </div>
      {/* p0222: the agent's one-sentence intent for this turn, when narrated. */}
      {event.intent && (
        <span className="pl-5 italic text-stone-400" data-testid="tool-call-intent">
          {event.intent}
        </span>
      )}
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
