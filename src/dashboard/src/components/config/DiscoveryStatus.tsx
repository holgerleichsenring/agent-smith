"use client";

import type { ConnectionRepos } from "@/lib/configApi";

// 2026-10-02-5f89c: a connection's discovery at a glance — the last success with its repo count,
// the last failed attempt with its reason beside it (a failure never erases the success), and
// Refresh now. The reason is the server's sentence.
// 2026-10-02-b540: a local answer — the shared store had nothing, this server's own last good list
// answered — says so, with that list's time.

export function DiscoveryStatus({
  testId,
  discovered,
  refreshing,
  refreshError,
  onRefresh,
}: {
  testId: string;
  discovered: ConnectionRepos | null;
  refreshing: boolean;
  refreshError: Error | null;
  onRefresh: () => void;
}) {
  const at = (iso: string) => new Date(iso).toLocaleString();
  const count = discovered?.repoCount ?? discovered?.repos.length ?? 0;
  return (
    <div className="picks" data-testid={`${testId}-status`} style={{ alignItems: "baseline", gap: 10 }}>
      {discovered?.discovering ? (
        <span className="help" data-testid={`${testId}-discovering`}>discovering…</span>
      ) : discovered?.discoveredAt && discovered.source === "local" ? (
        <span className="help" data-testid={`${testId}-local`}>
          from this server&apos;s last good list of {at(discovered.discoveredAt)} · {count} repos
        </span>
      ) : discovered?.discoveredAt ? (
        <span className="help" data-testid={`${testId}-success`}>
          last success {at(discovered.discoveredAt)} · {count} repos
        </span>
      ) : (
        <span className="help" data-testid={`${testId}-nosuccess`}>no successful discovery yet</span>
      )}
      {discovered?.lastError && (
        <span className="help" data-testid={`${testId}-lasterror`} style={{ color: "var(--bad)" }}>
          last attempt{discovered.lastAttemptAt ? ` ${at(discovered.lastAttemptAt)}` : ""} failed: {discovered.lastError}
        </span>
      )}
      <button
        type="button"
        className="pick"
        disabled={refreshing}
        onClick={onRefresh}
        data-testid={`${testId}-refresh`}
      >
        {refreshing ? "Refreshing…" : "Refresh now"}
      </button>
      {refreshError && (
        <span className="help" data-testid={`${testId}-refresh-error`} style={{ color: "var(--bad)" }}>
          refresh failed: {refreshError.message}
        </span>
      )}
    </div>
  );
}
