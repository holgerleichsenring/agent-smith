"use client";

import { useMemo, useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";
import { isRule } from "@/lib/repoRefs";
import type { StudioEntity } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { DiscoveryStatus } from "./DiscoveryStatus";
import { DiscoveryNotice, FilterBox, matchesQuery, type AddOffer } from "./RepoPicker";
import { useConnectionDiscovery } from "./useConnectionDiscovery";

// 2026-11-24-f965: a PoC target's source repository, picked the way a project picks repos — a catalog repo, or a repo
// on a connection from that connection's discovery (the same snapshot, Refresh and notices as RepoPicker). It names ONE
// repository: the rows are radios, and a typed name is offered only without a wildcard, which the server refuses.
// The value is a catalog id or connection/name, as before.

const WINDOW = 25;

export function SourceRepoPicker({ label, help, value, repos, connections, testId, notes, onChange }: {
  label: string; help: string; value: string; repos: StudioEntity[]; connections: StudioEntity[]; testId: string;
  /** The field's required hint and the server's sentences, shown under the label. */
  notes?: ReactNode;
  onChange: (v: string) => void;
}) {
  const t = useT();
  const inCatalog = repos.some((r) => r.id === value);
  const [connection, setConnection] = useState(() => connectionOf(value, connections, inCatalog));
  const [filter, setFilter] = useState("");
  const [shown, setShown] = useState(WINDOW);
  const { discovered, error, loading, refreshing, refreshError, refresh } = useConnectionDiscovery(connection);

  const found = useMemo(
    () => [...(discovered?.repos ?? [])].sort((a, b) => a.name.localeCompare(b.name)),
    [discovered],
  );
  const query = filter.trim();
  const matches = useMemo(() => found.filter((r) => matchesQuery(r.name, query)), [found, query]);
  const rows = matches.slice(0, shown);
  const hidden = matches.length - rows.length;
  const hasList = !loading && !error && discovered?.discoveredAt != null && found.length > 0;
  const typed = `${connection}/${query}`;
  const offer: AddOffer | null = connection && query !== "" && !isRule(query) && typed !== value
    ? { ref: typed, kind: "pick", label: t("config.pocs.source.use", { ref: typed }) }
    : null;
  const retype = (v: string) => {
    setFilter(v);
    setShown(WINDOW);
  };
  const use = () => {
    if (!offer) return;
    onChange(offer.ref);
    retype("");
  };
  const filterBox = <FilterBox testId={testId} filter={filter} offer={offer} onFilter={retype} onAdd={use} />;

  return (
    <div className="field" data-testid={testId}>
      <label>
        {label}
        <span className="help">{help}</span>
      </label>
      {notes}
      <div className="picks">
        <span className={cn("pick", value && "on")} data-testid={`${testId}-selected`}>
          {value || t("config.pocs.source.none")}
          {value && (
            <button type="button" aria-label={t("config.repoPicker.removeAria", { ref: value })} data-testid={`${testId}-clear`}
              onClick={() => onChange("")}
              style={{ background: "none", border: 0, cursor: "pointer", color: "inherit", font: "inherit" }}>
              ×
            </button>
          )}
        </span>
      </div>
      {repos.length > 0 && (
        <div className="field">
          <label>{t("config.pocs.source.catalog")}</label>
          <select data-testid={`${testId}-catalog`} className="mono" value={inCatalog ? value : ""}
            onChange={(e) => e.target.value && onChange(e.target.value)}>
            <option value="">{t("config.pick")}</option>
            {repos.map((r) => <option key={r.id} value={r.id}>{r.id}</option>)}
          </select>
        </div>
      )}
      <div className="field">
        <label>{t("config.pocs.source.connection")}</label>
        <select data-testid={`${testId}-connection`} className="mono" value={connection}
          onChange={(e) => { setConnection(e.target.value); retype(""); }}>
          <option value="">{t("config.pick")}</option>
          {connections.map((c) => <option key={c.id} value={c.id}>{c.id}</option>)}
        </select>
      </div>
      {connection && (
        <div className="field">
          <label>{t("config.repoPicker.discovered")}</label>
          {!loading && !error && (
            <DiscoveryStatus testId={testId} discovered={discovered} refreshing={refreshing} refreshError={refreshError}
              onRefresh={refresh} />
          )}
          {!hasList && <DiscoveryNotice testId={testId} loading={loading} error={error} discovered={discovered} />}
          {filterBox}
          {query !== "" && isRule(query) && (
            <span className="help" data-testid={`${testId}-no-wildcard`}>{t("config.pocs.source.noWildcard")}</span>
          )}
          {hasList && (
            <>
              <span className="help" data-testid={`${testId}-count`}>
                {t("config.repoPicker.count", { shown: rows.length, matched: matches.length, discovered: found.length })}
                {hidden > 0 ? t("config.repoPicker.hidden", { hidden }) : ""}
              </span>
              {matches.length === 0 ? (
                <span className="help" data-testid={`${testId}-nomatch`}>{t("config.repoPicker.noMatch", { query })}</span>
              ) : (
                <div className="repo-rows" role="radiogroup" aria-label={label} data-testid={`${testId}-rows`}>
                  {rows.map((r) => {
                    const ref = `${connection}/${r.name}`;
                    const on = value === ref;
                    return (
                      <label key={r.name} className={cn("repo-row", on && "on")} data-testid={`${testId}-row-${r.name}`}>
                        <input type="radio" name={`${testId}-repo`} checked={on} data-testid={`${testId}-discovered-${r.name}`}
                          onChange={() => onChange(ref)} />
                        <span className="rr-name">{r.name}</span>
                        <span className="rr-branch">{r.defaultBranch ?? "—"}</span>
                      </label>
                    );
                  })}
                </div>
              )}
              {hidden > 0 && (
                <button type="button" className="pick more" data-testid={`${testId}-more`} onClick={() => setShown(shown + WINDOW)}>
                  {t("config.repoPicker.more", { count: Math.min(WINDOW, hidden) })}
                </button>
              )}
            </>
          )}
        </div>
      )}
    </div>
  );
}

/** A connection/name value opens on its connection, so the picked row shows among the discovered ones. */
function connectionOf(value: string, connections: StudioEntity[], inCatalog: boolean): string {
  if (inCatalog) return "";
  const slash = value.indexOf("/");
  if (slash < 0) return "";
  const prefix = value.slice(0, slash);
  return connections.some((c) => c.id === prefix) ? prefix : "";
}
