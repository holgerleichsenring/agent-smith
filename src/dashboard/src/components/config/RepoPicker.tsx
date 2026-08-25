"use client";

import { useEffect, useMemo, useState } from "react";
import { cn } from "@/lib/utils";
import { isRule, patternMatches, refMatches } from "@/lib/repoRefs";
import {
  fetchConnectionRepos,
  type ConnectionRepos,
  type DiscoveredRepo,
  type StudioEntity,
} from "@/lib/configApi";

// p0345c: the REAL repo picker for connection-scoped project refs. Pick a
// connection → its discovery cache (GET /connections/{id}/repos) lists the
// repos that actually exist there; when the cache is empty (discoveredAt null)
// the picker says so honestly and falls back to typing a name.
//
// p0488: a connection with three hundred repos used to render three hundred
// chips. The discovered repos are ROWS now — filtered, rendered in a capped
// scroll window that always states matched-of-total, never truncated silently.
// The filter box is also the wildcard box: a pattern offers itself as a RULE
// (conn/Pre*) with the count it currently matches, and every discovered repo a
// rule already covers reads as covered instead of offering a redundant pick.

/** How many matching rows render before "show more" is offered. */
const WINDOW = 25;

type AddOffer = { ref: string; kind: "rule" | "pick"; label: string };

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
  const [filter, setFilter] = useState("");
  const [shown, setShown] = useState(WINDOW);
  const { discovered, error, loading } = useConnectionDiscovery(connection);

  const repos = useMemo(
    () => [...(discovered?.repos ?? [])].sort((a, b) => a.name.localeCompare(b.name)),
    [discovered],
  );
  const query = filter.trim();
  const matches = useMemo(() => repos.filter((r) => matchesQuery(r.name, query)), [repos, query]);
  const rows = matches.slice(0, shown);
  const hidden = matches.length - rows.length;
  const hasList = !loading && !error && discovered?.discoveredAt != null && repos.length > 0;

  const rules = values.filter(isRule);
  const ruleFor = (name: string) => rules.find((r) => refMatches(r, connection, name));
  const offer = buildOffer(connection, query, values, matches.length, discovered?.discoveredAt != null);

  const retype = (v: string) => {
    setFilter(v);
    setShown(WINDOW);
  };
  const toggle = (ref: string) =>
    onChange(values.includes(ref) ? values.filter((v) => v !== ref) : [...values, ref]);
  const add = () => {
    if (!offer) return;
    onChange([...values, offer.ref]);
    retype("");
  };
  const selectAllFiltered = () => {
    if (query !== "" && isRule(query)) return add();
    const additions = matches
      .filter((r) => !ruleFor(r.name) && !values.includes(`${connection}/${r.name}`))
      .map((r) => `${connection}/${r.name}`);
    if (additions.length > 0) onChange([...values, ...additions]);
  };

  const filterBox = <FilterBox testId={testId} filter={filter} offer={offer} onFilter={retype} onAdd={add} />;

  return (
    <div className="field" data-testid={testId}>
      <label>{label}</label>
      <SelectedRefs testId={testId} values={values} connection={connection} repos={repos} onChange={onChange} />

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
          {hasList ? (
            <>
              {filterBox}
              <div className="repo-head">
                <span className="help" data-testid={`${testId}-count`}>
                  showing {rows.length} of {matches.length} matched · {repos.length} discovered
                  {hidden > 0 ? ` · ${hidden} hidden` : ""}
                </span>
                {matches.length > 0 && (
                  <button
                    type="button"
                    className="pick"
                    data-testid={`${testId}-select-all`}
                    onClick={selectAllFiltered}
                  >
                    {query !== "" && isRule(query)
                      ? `select all ${matches.length} as rule ${connection}/${query}`
                      : `select all ${matches.length}`}
                  </button>
                )}
              </div>
              {matches.length === 0 ? (
                <span className="help" data-testid={`${testId}-nomatch`}>
                  no discovered repo matches “{query}”
                </span>
              ) : (
                <RepoRows
                  testId={testId}
                  rows={rows}
                  connection={connection}
                  values={values}
                  ruleFor={ruleFor}
                  onToggle={toggle}
                />
              )}
              {hidden > 0 && (
                <button
                  type="button"
                  className="pick more"
                  data-testid={`${testId}-more`}
                  onClick={() => setShown(shown + WINDOW)}
                >
                  show {Math.min(WINDOW, hidden)} more
                </button>
              )}
            </>
          ) : (
            <>
              <DiscoveryNotice testId={testId} loading={loading} error={error} discovered={discovered} />
              {filterBox}
            </>
          )}
        </div>
      )}
    </div>
  );
}

/** The discovery cache of the picked connection, with its own loading/error state. */
function useConnectionDiscovery(connectionId: string) {
  const [discovered, setDiscovered] = useState<ConnectionRepos | null>(null);
  const [error, setError] = useState<Error | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setDiscovered(null);
    setError(null);
    if (!connectionId) return;
    const controller = new AbortController();
    setLoading(true);
    fetchConnectionRepos(connectionId, controller.signal)
      .then((r) => setDiscovered(r))
      .catch((err: Error) => {
        if (err.name !== "AbortError") setError(err);
      })
      .finally(() => setLoading(false));
    return () => controller.abort();
  }, [connectionId]);

  return { discovered, error, loading };
}

/** Free text narrows the list; a wildcard narrows it to exactly what the rule would cover. */
function matchesQuery(repoName: string, query: string): boolean {
  if (query === "") return true;
  if (isRule(query)) return patternMatches(query, repoName);
  return repoName.toLowerCase().includes(query.toLowerCase());
}

/** What the filter box currently offers to add — a rule when it reads as one, a pick otherwise. */
function buildOffer(
  connection: string,
  query: string,
  values: string[],
  matchCount: number,
  counted: boolean,
): AddOffer | null {
  if (!connection || query === "") return null;
  const ref = `${connection}/${query}`;
  if (values.includes(ref)) return null;
  if (!isRule(query)) return { ref, kind: "pick", label: `add ${ref}` };
  return { ref, kind: "rule", label: `add rule ${ref}${counted ? ` · ${matchCount} match` : ""}` };
}

/** The refs already on the project: a rule carries its live match count, a pick does not. */
function SelectedRefs({
  testId,
  values,
  connection,
  repos,
  onChange,
}: {
  testId: string;
  values: string[];
  connection: string;
  repos: DiscoveredRepo[];
  onChange: (v: string[]) => void;
}) {
  const covers = (ref: string) => repos.filter((r) => refMatches(ref, connection, r.name)).length;
  return (
    <div className="picks">
      {values.length === 0 && <span className="help">no connection-scoped repos</span>}
      {values.map((ref) => (
        <span
          key={ref}
          data-testid={`${testId}-chip-${ref}`}
          data-kind={isRule(ref) ? "rule" : "pick"}
          className="pick on"
        >
          {ref}
          {isRule(ref) && ref.startsWith(`${connection}/`) && repos.length > 0 && (
            <span className="rk" data-testid={`${testId}-rulecount-${ref}`}>
              {covers(ref)} match
            </span>
          )}
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
  );
}

/** p0345c's honest discovery states — loading, unavailable, never discovered, discovered-but-empty. */
function DiscoveryNotice({
  testId,
  loading,
  error,
  discovered,
}: {
  testId: string;
  loading: boolean;
  error: Error | null;
  discovered: ConnectionRepos | null;
}) {
  if (loading)
    return (
      <span className="help" data-testid={`${testId}-loading`}>
        loading discovery cache…
      </span>
    );
  if (error)
    return (
      <span className="help" data-testid={`${testId}-error`} style={{ color: "var(--bad)" }}>
        discovery cache unavailable: {error.message}
      </span>
    );
  if (discovered?.discoveredAt === null)
    return (
      <span className="help" data-testid={`${testId}-undiscovered`}>
        not discovered yet — run a discovery or type a name below
      </span>
    );
  if (discovered && discovered.repos.length === 0)
    return (
      <span className="help" data-testid={`${testId}-none`}>
        discovery ran but found no repos in this connection
      </span>
    );
  return null;
}

/** The capped window of discovered repos: checkbox, name, default branch — unless a rule covers it. */
function RepoRows({
  testId,
  rows,
  connection,
  values,
  ruleFor,
  onToggle,
}: {
  testId: string;
  rows: DiscoveredRepo[];
  connection: string;
  values: string[];
  ruleFor: (name: string) => string | undefined;
  onToggle: (ref: string) => void;
}) {
  return (
    <div className="repo-rows" data-testid={`${testId}-rows`}>
      {rows.map((r) => {
        const ref = `${connection}/${r.name}`;
        const rule = ruleFor(r.name);
        const on = values.includes(ref);
        return (
          <div
            key={r.name}
            className={cn("repo-row", on && !rule && "on", rule && "covered")}
            data-testid={`${testId}-row-${r.name}`}
            data-state={rule ? "covered" : on ? "picked" : "free"}
          >
            {rule ? (
              <span className="rr-lock" aria-hidden="true">
                ↳
              </span>
            ) : (
              <input
                type="checkbox"
                data-testid={`${testId}-discovered-${r.name}`}
                data-selected={on ? "true" : "false"}
                aria-label={r.name}
                checked={on}
                onChange={() => onToggle(ref)}
              />
            )}
            <span className="rr-name">{r.name}</span>
            <span className="rr-branch">{r.defaultBranch ?? "—"}</span>
            {rule && (
              <span className="rr-rule" data-testid={`${testId}-covered-${r.name}`}>
                covered by {rule}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}

/** One box for two thoughts that were always one: narrow the list, or add what was typed. */
function FilterBox({
  testId,
  filter,
  offer,
  onFilter,
  onAdd,
}: {
  testId: string;
  filter: string;
  offer: AddOffer | null;
  onFilter: (v: string) => void;
  onAdd: () => void;
}) {
  return (
    <div className="repo-filter">
      <input
        type="text"
        data-testid={`${testId}-filter`}
        aria-label="filter discovered repos"
        value={filter}
        placeholder="filter — or a wildcard, e.g. Sample.*"
        className="mono"
        onChange={(e) => onFilter(e.target.value)}
      />
      <button
        type="button"
        className={cn("pick", offer?.kind === "rule" && "on")}
        data-testid={`${testId}-add`}
        disabled={!offer}
        onClick={onAdd}
        style={!offer ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
      >
        {offer ? offer.label : "Add"}
      </button>
    </div>
  );
}
