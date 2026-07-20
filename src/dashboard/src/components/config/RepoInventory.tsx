"use client";

import { useEffect, useState } from "react";
import {
  fetchConnectionRepos,
  type ConnectionRepos,
  type StudioConnection,
  type StudioProject,
} from "@/lib/configApi";

// p0345c: the DISCOVERED half of the Repositories page — one section per
// connection listing what the discovery cache actually found there, read-only,
// with "referenced by <project>" badges wherever a project wires conn/Name
// (exact ref or wildcard). A connection whose cache is empty says so honestly
// instead of rendering blank.

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
  const [snapshot, setSnapshot] = useState<ConnectionRepos | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchConnectionRepos(connection.id, controller.signal)
      .then(setSnapshot)
      .catch((err: Error) => {
        if (err.name !== "AbortError") setError(err.message);
      });
    return () => controller.abort();
  }, [connection.id]);

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
      {error ? (
        <div className="fields">
          <div className="f" data-testid={`repo-inventory-error-${connection.id}`}>
            <span className="fl">discovery</span>
            <span className="fv" style={{ color: "var(--bad)" }}>
              unavailable: {error}
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
            <span className="fv">not discovered yet — run a discovery or type a name on the project</span>
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

/** Does a project repo ref ("conn/Name", "conn/*", "conn/Pre*") cover this
 *  discovered repo? Plain catalog refs (no slash) never match here. */
export function refMatches(ref: string, connectionId: string, repoName: string): boolean {
  const slash = ref.indexOf("/");
  if (slash <= 0 || ref.slice(0, slash) !== connectionId) return false;
  const pattern = ref.slice(slash + 1);
  if (!pattern.includes("*")) return pattern === repoName;
  const rx = new RegExp(
    `^${pattern
      .split("*")
      .map((s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))
      .join(".*")}$`,
  );
  return rx.test(repoName);
}
