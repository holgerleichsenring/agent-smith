"use client";

import { useMemo, useState } from "react";
import { cn } from "@/lib/utils";
import { isRule, patternMatches, refMatches } from "@/lib/repoRefs";
import { type ConnectionRepos, type DiscoveredRepo, type StudioEntity } from "@/lib/configApi";
import { DiscoveryStatus } from "./DiscoveryStatus";
import { NO_AUTOFILL } from "./formFields";
import { useConnectionDiscovery } from "./useConnectionDiscovery";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

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

export type AddOffer = { ref: string; kind: "rule" | "pick"; label: string };

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
  const t = useT();
  const [connection, setConnection] = useState("");
  const [filter, setFilter] = useState("");
  const [shown, setShown] = useState(WINDOW);
  const { discovered, error, loading, refreshing, refreshError, refresh } = useConnectionDiscovery(connection);

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
  const offer = buildOffer(t, connection, query, values, matches.length, discovered?.discoveredAt != null);

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
        <label>{t("config.repoPicker.connection")}</label>
        <select
          data-testid={`${testId}-connection`}
          value={connection}
          onChange={(e) => setConnection(e.target.value)}
          className="mono"
        >
          <option value="">{t("config.pick")}</option>
          {connections.map((c) => (
            <option key={c.id} value={c.id}>
              {c.id}
            </option>
          ))}
        </select>
      </div>

      {connection && (
        <div className="field">
          <label>{t("config.repoPicker.discovered")}</label>
          {!loading && !error && (
            <DiscoveryStatus
              testId={testId}
              discovered={discovered}
              refreshing={refreshing}
              refreshError={refreshError}
              onRefresh={refresh}
            />
          )}
          {hasList ? (
            <>
              {filterBox}
              <div className="repo-head">
                <span className="help" data-testid={`${testId}-count`}>
                  {t("config.repoPicker.count", { shown: rows.length, matched: matches.length, discovered: repos.length })}
                  {hidden > 0 ? t("config.repoPicker.hidden", { hidden }) : ""}
                </span>
                {matches.length > 0 && (
                  <button
                    type="button"
                    className="pick"
                    data-testid={`${testId}-select-all`}
                    onClick={selectAllFiltered}
                  >
                    {query !== "" && isRule(query)
                      ? t("config.repoPicker.selectAllRule", { count: matches.length, rule: `${connection}/${query}` })
                      : t("config.repoPicker.selectAll", { count: matches.length })}
                  </button>
                )}
              </div>
              {matches.length === 0 ? (
                <span className="help" data-testid={`${testId}-nomatch`}>
                  {t("config.repoPicker.noMatch", { query })}
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
                  {t("config.repoPicker.more", { count: Math.min(WINDOW, hidden) })}
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

/** Free text narrows the list; a wildcard narrows it to exactly what the rule would cover. */
export function matchesQuery(repoName: string, query: string): boolean {
  if (query === "") return true;
  if (isRule(query)) return patternMatches(query, repoName);
  return repoName.toLowerCase().includes(query.toLowerCase());
}

/** What the filter box currently offers to add — a rule when it reads as one, a pick otherwise. */
function buildOffer(
  t: T,
  connection: string,
  query: string,
  values: string[],
  matchCount: number,
  counted: boolean,
): AddOffer | null {
  if (!connection || query === "") return null;
  const ref = `${connection}/${query}`;
  if (values.includes(ref)) return null;
  if (!isRule(query)) return { ref, kind: "pick", label: t("config.repoPicker.add", { ref }) };
  return {
    ref,
    kind: "rule",
    label: counted ? t("config.repoPicker.addRuleCounted", { ref, count: matchCount }) : t("config.repoPicker.addRule", { ref }),
  };
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
  const t = useT();
  const covers = (ref: string) => repos.filter((r) => refMatches(ref, connection, r.name)).length;
  return (
    <div className="picks">
      {values.length === 0 && <span className="help">{t("config.repoPicker.noneSelected")}</span>}
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
              {t("config.repoPicker.match", { count: covers(ref) })}
            </span>
          )}
          <button
            type="button"
            aria-label={t("config.repoPicker.removeAria", { ref })}
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

/** p0345c's honest discovery states — loading, unavailable, never discovered, discovered-but-empty.
 *  2026-11-24-f965: shared with the PoC source repository picker. */
export function DiscoveryNotice({
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
  const t = useT();
  if (loading)
    return (
      <span className="help" data-testid={`${testId}-loading`}>
        {t("config.repoPicker.loading")}
      </span>
    );
  if (error)
    return (
      <span className="help" data-testid={`${testId}-error`} style={{ color: "var(--bad)" }}>
        {t("config.repoPicker.unavailable", { message: error.message })}
      </span>
    );
  if (discovered?.discoveredAt === null)
    return (
      <span className="help" data-testid={`${testId}-undiscovered`}>
        {discovered.discovering ? t("config.discovery.discovering") : t("config.repoPicker.notYet")}
      </span>
    );
  if (discovered && discovered.repos.length === 0)
    return (
      <span className="help" data-testid={`${testId}-none`}>
        {t("config.repoPicker.none")}
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
  const t = useT();
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
                {t("config.repoPicker.coveredBy", { rule })}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
}

/** One box for two thoughts that were always one: narrow the list, or add what was typed.
 *  2026-11-24-f965: shared with the PoC source repository picker; browser autofill stays off. */
export function FilterBox({
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
  const t = useT();
  return (
    <div className="repo-filter">
      <input
        type="text"
        data-testid={`${testId}-filter`}
        name={`${testId}-filter`}
        {...NO_AUTOFILL}
        aria-label={t("config.repoPicker.filterAria")}
        value={filter}
        placeholder={t("config.repoPicker.filterPlaceholder")}
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
        {offer ? offer.label : t("config.repoPicker.addButton")}
      </button>
    </div>
  );
}
