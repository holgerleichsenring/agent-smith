import { useMemo } from "react";
import type { SystemEvent } from "@/types/system-events";

// p0173d: per-source event counts. Source field is free-form
// ("tracker:jira/foo", "webhook:github", "chat:slack", "config-loader",
// "skill-catalog", "concept-vocabulary"). Dashboard renders the
// breakdown as a sortable list — operator sees "which producers are
// active right now" at a glance.

export interface ChannelBreakdownRow {
  source: string;
  count: number;
}

export function useChannelBreakdown(events: readonly SystemEvent[]): ChannelBreakdownRow[] {
  return useMemo(() => deriveChannelBreakdown(events), [events]);
}

export function deriveChannelBreakdown(events: readonly SystemEvent[]): ChannelBreakdownRow[] {
  const counts = new Map<string, number>();
  for (const e of events) {
    counts.set(e.source, (counts.get(e.source) ?? 0) + 1);
  }
  return [...counts.entries()]
    .map(([source, count]) => ({ source, count }))
    .sort((a, b) => b.count - a.count || a.source.localeCompare(b.source));
}
