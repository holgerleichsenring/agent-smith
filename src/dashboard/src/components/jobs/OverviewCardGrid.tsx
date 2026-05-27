"use client";

import { HubConnectionState } from "@microsoft/signalr";
import { useJobsHub } from "@/hooks/useJobsHub";
import { ConnectionState } from "./ConnectionState";
import { RunCard } from "./RunCard";

export function OverviewCardGrid() {
  const { connectionState, overview } = useJobsHub();

  if (overview === null && connectionState !== HubConnectionState.Connected) {
    return (
      <div className="space-y-4" data-testid="overview-skeleton">
        <ConnectionState state={connectionState} />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-24 animate-pulse rounded-lg border border-stone-200 bg-stone-100" />
          ))}
        </div>
      </div>
    );
  }

  if (overview === null || (overview.active.length === 0 && overview.recent.length === 0)) {
    return (
      <div className="space-y-4">
        <ConnectionState state={connectionState} />
        <p className="text-sm text-stone-500" data-testid="overview-empty">
          No runs yet. Trigger one via the CLI, a webhook, or a poller.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <ConnectionState state={connectionState} />
      {overview.active.length > 0 && (
        <section data-testid="overview-active">
          <h2 className="mb-3 text-sm font-medium text-stone-700">Active ({overview.active.length})</h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {overview.active.map((r) => <RunCard key={r.runId} snapshot={r} />)}
          </div>
        </section>
      )}
      {overview.recent.length > 0 && (
        <section data-testid="overview-recent">
          <h2 className="mb-3 text-sm font-medium text-stone-700">Recent ({overview.recent.length})</h2>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {overview.recent.map((r) => <RunCard key={r.runId} snapshot={r} />)}
          </div>
        </section>
      )}
    </div>
  );
}
