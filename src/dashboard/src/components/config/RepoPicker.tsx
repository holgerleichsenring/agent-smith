"use client";

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import { fetchConnectionRepos, type ConnectionRepos, type StudioEntity } from "@/lib/configApi";

// p0345c: the REAL repo picker for connection-scoped project refs. Pick a
// connection → its discovery cache (GET /connections/{id}/repos) lists the
// repos that actually exist there as multi-select .pick chips; a wildcard/glob
// input keeps "conn/*" possible; and when the cache is empty (discoveredAt
// null) the picker says so honestly and falls back to typing a name. Selected
// refs render as the existing removable chips.

export function RepoPicker({
  label,
  values,
  connections,
  onChange,
  testId = "form-connref",
}: {
  label: string;
  values: string[];
  connections: StudioEntity[];
  onChange: (v: string[]) => void;
  testId?: string;
}) {
  const [connection, setConnection] = useState("");
  const [pattern, setPattern] = useState("");
  const [discovered, setDiscovered] = useState<ConnectionRepos | null>(null);
  const [discoveryError, setDiscoveryError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setDiscovered(null);
    setDiscoveryError(null);
    if (!connection) return;
    const controller = new AbortController();
    setLoading(true);
    fetchConnectionRepos(connection, controller.signal)
      .then((r) => setDiscovered(r))
      .catch((err: Error) => {
        if (err.name !== "AbortError") setDiscoveryError(err.message);
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [connection]);

  const toggle = (ref: string) =>
    onChange(values.includes(ref) ? values.filter((v) => v !== ref) : [...values, ref]);

  const candidate = connection && pattern.trim() ? `${connection}/${pattern.trim()}` : null;
  const addPattern = () => {
    if (!candidate || values.includes(candidate)) return;
    onChange([...values, candidate]);
    setPattern("");
  };

  const notDiscovered = discovered !== null && discovered.discoveredAt === null;

  return (
    <div className="field" data-testid={testId}>
      <label>{label}</label>
      <div className="picks">
        {values.length === 0 && <span className="help">no connection-scoped repos</span>}
        {values.map((ref) => (
          <span key={ref} data-testid={`${testId}-chip-${ref}`} className="pick on">
            {ref}
            <button
              type="button"
              aria-label={`Remove ${ref}`}
              data-testid={`${testId}-remove-${ref}`}
              onClick={() => onChange(values.filter((v) => v !== ref))}
              style={{ background: "none", border: 0, cursor: "pointer", color: "inherit", font: "inherit" }}
            >
              ×
            </button>
          </span>
        ))}
      </div>

      <div className="field">
        <label>connection</label>
        <select
          data-testid={`${testId}-connection`}
          value={connection}
          onChange={(e) => setConnection(e.target.value)}
          className="mono"
        >
          <option value="">— pick —</option>
          {connections.map((c) => (
            <option key={c.id} value={c.id}>
              {c.id}
            </option>
          ))}
        </select>
      </div>

      {connection && (
        <div className="field">
          <label>
            discovered repos
            {discovered?.discoveredAt && (
              <span className="help">discovered {new Date(discovered.discoveredAt).toLocaleString()}</span>
            )}
          </label>
          {loading ? (
            <span className="help" data-testid={`${testId}-loading`}>
              loading discovery cache…
            </span>
          ) : discoveryError ? (
            <span className="help" data-testid={`${testId}-error`} style={{ color: "var(--bad)" }}>
              discovery cache unavailable: {discoveryError}
            </span>
          ) : notDiscovered ? (
            <span className="help" data-testid={`${testId}-undiscovered`}>
              not discovered yet — run a discovery or type a name below
            </span>
          ) : discovered && discovered.repos.length === 0 ? (
            <span className="help" data-testid={`${testId}-none`}>
              discovery ran but found no repos in this connection
            </span>
          ) : discovered ? (
            <div className="picks">
              {discovered.repos.map((r) => {
                const ref = `${connection}/${r.name}`;
                const on = values.includes(ref);
                return (
                  <button
                    key={r.name}
                    type="button"
                    data-testid={`${testId}-discovered-${r.name}`}
                    data-selected={on ? "true" : "false"}
                    aria-pressed={on}
                    onClick={() => toggle(ref)}
                    className={cn("pick", on && "on")}
                    title={r.defaultBranch ? `default branch ${r.defaultBranch}` : undefined}
                  >
                    <span className="pk">{on ? "✓" : ""}</span>
                    {r.name}
                  </button>
                );
              })}
            </div>
          ) : null}
        </div>
      )}

      <div style={{ display: "flex", gap: 9, alignItems: "flex-end" }}>
        <div className="field" style={{ flex: 1 }}>
          <label>
            name or wildcard
            <span className="help">e.g. Sample.Api or *</span>
          </label>
          <input
            type="text"
            data-testid={`${testId}-name`}
            value={pattern}
            placeholder="RepoName or *"
            className="mono"
            onChange={(e) => setPattern(e.target.value)}
          />
        </div>
        <button
          type="button"
          className="pick"
          data-testid={`${testId}-add`}
          disabled={!candidate}
          onClick={addPattern}
          style={!candidate ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
        >
          Add
        </button>
      </div>
    </div>
  );
}
