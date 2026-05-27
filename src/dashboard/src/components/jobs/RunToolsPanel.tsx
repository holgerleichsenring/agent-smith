"use client";

import { useMemo } from "react";
import { useEventFilter } from "@/lib/EventFilterContext";
import { pairToolEvents } from "@/lib/ToolEventPairer";
import { isAllowed } from "@/lib/eventFilterQuery";
import { EventType, type RunEvent } from "@/types/hub-events";
import { ToolPairCard } from "./ToolPairCard";
import { ToolCallRow } from "./ToolCallRow";
import { ToolResultRow } from "./ToolResultRow";
import { ToolCountChip } from "./ToolCountChip";

interface Props {
  events: RunEvent[];
}

// p0169g: tool events render at the run level, not per-sandbox.
// ToolCall / ToolResult events don't carry a repo field today
// (EventPublishingAIFunction wraps AIFunction calls without sandbox
// context), so attribution to a specific SandboxBox isn't possible.
// Surfacing them on the run-detail surface keeps p0169e's contract
// honest. A follow-up could enrich the events with repo for per-sandbox
// rendering.

export function RunToolsPanel({ events }: Props) {
  const { state: filterState } = useEventFilter();

  const filtered = useMemo(
    () => events.filter((e) =>
      (e.type === EventType.ToolCall || e.type === EventType.ToolResult)
      && isAllowed(filterState, e.type)),
    [events, filterState],
  );
  const rows = useMemo(() => pairToolEvents(filtered), [filtered]);
  const callCount = useMemo(
    () => filtered.filter((e) => e.type === EventType.ToolCall).length,
    [filtered],
  );

  const l3Off = filterState.l3.size === 0;

  if (l3Off) {
    return (
      <section className="space-y-2 text-xs text-stone-500" data-testid="run-tools-panel-l3-off">
        L3 events hidden. Toggle ToolCall / ToolResult in the filter rail to view tool activity.
      </section>
    );
  }

  if (rows.length === 0) {
    return (
      <section className="space-y-2 text-xs text-stone-500" data-testid="run-tools-panel-empty">
        No tool events yet.
      </section>
    );
  }

  return (
    <section className="space-y-2 rounded-lg border border-stone-200 bg-stone-950 p-3" data-testid="run-tools-panel">
      <header className="flex items-center justify-between text-xs">
        <h2 className="font-medium text-stone-300">Tools</h2>
        <ToolCountChip count={callCount} />
      </header>
      <div className="space-y-1 font-mono">
        {rows.map((row, idx) => row.kind === "pair"
          ? <ToolPairCard key={`pair-${idx}`} call={row.call} result={row.result} />
          : row.kind === "call-only"
            ? <ToolCallRow key={`call-${idx}`} event={row.call} />
            : <ToolResultRow key={`result-${idx}`} event={row.result} />)}
      </div>
    </section>
  );
}
