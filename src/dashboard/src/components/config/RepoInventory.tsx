"use client";

import { refMatches } from "@/lib/repoRefs";
import { type StudioConnection, type StudioProject } from "@/lib/configApi";
import { DiscoveryStatus } from "./DiscoveryStatus";
import { useConnectionDiscovery } from "./useConnectionDiscovery";

// p0345c: the DISCOVERED half of the Repositories page — one section per
// connection listing what the discovery cache actually found there, read-only,
// with "referenced by <project>" badges wherever a project wires conn/Name
// (exact ref or wildcard). A connection whose cache is empty says so honestly
// instead of rendering blank.
// 2026-10-02-5f89c: each section states its last success, its last error with the reason,
// and offers Refresh now, after which it re-reads.

export function RepoInventory({
  connections,
  projects,
}: {
  connections: StudioConnection[];
  projects: StudioProject[];
}) {
  if (connections.length === 0) {
    return (
      <div className="empty" data-testid="repo-inventory-empty">
        <div className="ei">◳</div>
        No connections configured — discovered inventory appears per connection.
      </div>
    );
  }
  return (
    <div className="list" data-testid="repo-inventory">
      {connections.map((c) => (
        <ConnectionInventory key={c.id} connection={c} projects={projects} />
      ))}
    </div>
  );
}

function ConnectionInventory({
  connection,
  projects,
}: {
  connection: StudioConnection;
  projects: StudioProject[];
}) {
  const { discovered: snapshot, error, refreshing, refreshError, refresh } = useConnectionDiscovery(connection.id);

  return (
    <div className="ecard" data-testid={`repo-inventory-${connection.id}`}>
      <div className="ec-top">
        <div className="ec-ic">◳</div>
        <div>
          <div className="ec-name">{connection.id}</div>
          <div className="ec-sub">
            {snapshot?.discoveredAt
              ? `discovered ${new Date(snapshot.discoveredAt).toLocaleString()}`
              : "discovery cache"}
          </div>
        </div>
        <div className="ec-right">
          <span className="tybadge">{connection.type || "connection"}</span>
        </div>
      </div>
      <DiscoveryStatus
        testId={`repo-inventory-${connection.id}`}
        discovered={snapshot}
        refreshing={refreshing}
        refreshError={refreshError}
        onRefresh={refresh}
      />
      {error ? (
        <div className="fields">
          <div className="f" data-testid={`repo-inventory-error-${connection.id}`}>
            <span className="fl">discovery</span>
            <span className="fv" style={{ color: "var(--bad)" }}>
              unavailable: {error.message}
            </span>
          </div>
        </div>
      ) : snapshot === null ? (
        <div className="fields">
          <div className="f">
            <span className="fl">discovery</span>
            <span className="fv">loading…</span>
          </div>
        </div>
      ) : snapshot.discoveredAt === null ? (
        <div className="fields">
          <div className="f" data-testid={`repo-inventory-undiscovered-${connection.id}`}>
            <span className="fl">discovery</span>
            <span className="fv">
              {snapshot.discovering ? "discovering…" : "not discovered yet — Refresh now, or type a name on the project"}
            </span>
          </div>
        </div>
      ) : (
        <div className="fields" style={{ flexDirection: "column", alignItems: "stretch" }}>
          {snapshot.repos.length === 0 && (
            <div className="f" data-testid={`repo-inventory-none-${connection.id}`}>
              <span className="fl">discovery</span>
              <span className="fv">ran, but found no repos in this connection</span>
            </div>
          )}
          {[...snapshot.repos].sort((a, b) => a.name.localeCompare(b.name)).map((r) => {
            const referencedBy = projects
              .filter((p) => p.repos.some((ref) => refMatches(ref, connection.id, r.name)))
              .map((p) => p.id);
            return (
              <div
                key={r.name}
                className="f"
                style={{ flexDirection: "row", alignItems: "baseline", gap: 10, borderRight: 0 }}
                data-testid={`repo-inventory-${connection.id}-${r.name}`}
              >
                <span className="fv">{r.name}</span>
                {r.defaultBranch && <span className="fl">branch {r.defaultBranch}</span>}
                {referencedBy.map((projectId) => (
                  <span
                    key={projectId}
                    className="tybadge"
                    data-testid={`repo-referenced-${connection.id}-${r.name}-${projectId}`}
                  >
                    referenced by {projectId}
                  </span>
                ))}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
