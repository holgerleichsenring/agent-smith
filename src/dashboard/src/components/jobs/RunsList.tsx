"use client";

import { useMemo, useState } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import type { RunSnapshot } from "@/types/hub-events";
import { ConnectionState } from "./ConnectionState";
import { RunRow } from "./RunRow";
import { RunFilterChips, type RunFilter } from "./RunFilterChips";
import { toNodeStatus } from "./runStatus";

// p0208: runs list container. Merges overview.active + recent into ONE list,
// newest-first by startedAt, then renders the Recent bar + filter chips + a
// single-column dense list. Supersedes OverviewCardGrid. Reuses useJobsHub.

function mergeNewestFirst(active: RunSnapshot[], recent: RunSnapshot[]): RunSnapshot[] {
  const byId = new Map<string, RunSnapshot>();
  // active first so a still-running snapshot wins over a stale recent dup.
  for (const r of [...active, ...recent]) {
    if (!byId.has(r.runId)) byId.set(r.runId, r);
  }
  return [...byId.values()].sort(
    (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime(),
  );
}

export function RunsList() {
  const { connectionState, overview } = useJobsHub();
  const [filter, setFilter] = useState<RunFilter>("all");

  const runs = useMemo(
    () => (overview ? mergeNewestFirst(overview.active, overview.recent) : []),
    [overview],
  );

  if (overview === null && connectionState !== HubConnectionState.Connected) {
    return (
      <div className="space-y-4" data-testid="runs-skeleton">
        <ConnectionState state={connectionState} />
        <div className="overflow-hidden rounded-xl border border-stone-200 bg-white">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-16 animate-pulse border-b border-stone-100 bg-stone-50 last:border-b-0" />
          ))}
        </div>
      </div>
    );
  }

  if (runs.length === 0) {
    return (
      <div className="space-y-4">
        <Bar runs={runs} filter={filter} onChange={setFilter} connectionState={connectionState} />
        <div
          className="rounded-xl border border-stone-200 bg-white p-10 text-center text-[14px] text-stone-400"
          data-testid="runs-empty"
        >
          No runs yet. Trigger one via the CLI, a webhook, or a poller.
        </div>
      </div>
    );
  }

  const filtered = filter === "all" ? runs : runs.filter((r) => toNodeStatus(r.status) === filter);

  return (
    <div className="space-y-4">
      <Bar runs={runs} filter={filter} onChange={setFilter} connectionState={connectionState} />
      {filtered.length === 0 ? (
        <div
          className="rounded-xl border border-stone-200 bg-white p-10 text-center text-[14px] text-stone-400"
          data-testid="runs-empty-filtered"
        >
          No runs match.
        </div>
      ) : (
        <div className="overflow-hidden rounded-xl border border-stone-200 bg-white" data-testid="runs-list">
          {filtered.map((r) => (
            <RunRow key={r.runId} snapshot={r} />
          ))}
        </div>
      )}
    </div>
  );
}

function Bar({
  runs,
  filter,
  onChange,
  connectionState,
}: {
  runs: RunSnapshot[];
  filter: RunFilter;
  onChange: (f: RunFilter) => void;
  connectionState: HubConnectionState;
}) {
  return (
    <div className="flex items-center gap-3">
      <span className="text-[14px] font-semibold text-stone-600">Recent</span>
      <div className="ml-auto flex items-center gap-4">
        <ConnectionState state={connectionState} />
        <RunFilterChips runs={runs} active={filter} onChange={onChange} />
      </div>
    </div>
  );
}
