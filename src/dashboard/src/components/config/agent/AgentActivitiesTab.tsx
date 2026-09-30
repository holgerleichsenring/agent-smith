"use client";

import type { ConfigCapabilities, ModelPriceList, StudioAgent } from "@/lib/configApi";
import {
  ACTIVITY,
  activityLabel,
  effectiveEntry,
  followsRole,
  resolvePrice,
  roleList,
  shortPrice,
} from "./modelCatalog";

// 2026-09-30-62bab: the Activities tab — each role the product routes picks an entry of
// the catalog (and only that); an optional role left unset follows another. A role that
// needs a strong model and resolves to one tiered fast is named — advice, not a refusal.

export function AgentActivitiesTab({
  draft,
  onChange,
  capabilities,
  prices,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
  capabilities: ConfigCapabilities | null;
  prices: ModelPriceList | null;
}) {
  const catalog = draft.catalog ?? {};
  const names = Object.keys(catalog);
  const list = prices?.models ?? null;
  const optionLabel = (name: string) => {
    const e = catalog[name];
    const price = prices ? ` · ${shortPrice(resolvePrice(e.model, draft.pricing, list))}` : "";
    return `${name} · ${e.tier ?? "no tier"}${price}`;
  };

  function assign(role: string, entry: string) {
    const models = { ...draft.models };
    if (entry) models[role] = entry;
    else delete models[role];
    onChange({ ...draft, models });
  }

  return (
    <>
      <span className="help">
        Each activity picks a model from the catalog. Unset, it follows Coding.{" "}
        <b>needs strong</b> marks an activity whose answer every later step builds on.
      </span>
      {names.length === 0 && (
        <div className="integrity warn" role="status" data-testid="agent-activities-empty">
          <span className="ii" aria-hidden="true">▲</span>
          <span>The catalog is empty — add a model on the Models tab, then pick it for Coding.</span>
        </div>
      )}
      {roleList(capabilities).map((r) => {
        const chosen = draft.models[r.key] ?? "";
        const declared = chosen === "" || chosen in catalog;
        const follows = followsRole(r.key, draft.models);
        const effective = effectiveEntry(r.key, draft.models);
        const mismatch = r.needsStrong && effective != null && catalog[effective]?.tier === "fast";
        const selectId = `agent-activity-select-${r.key}`;
        return (
          <div
            key={r.key}
            className="field"
            data-testid={`agent-activity-${r.key}`}
            style={{ paddingBottom: 10, borderBottom: "1px solid var(--line-2)" }}
          >
            <label htmlFor={selectId}>
              <span style={{ flex: 1 }}>
                {activityLabel(r.key)} <span className="mono help">{r.key}</span>
              </span>
              {r.needsStrong && (
                <span className="tier-pill strong" data-testid={`agent-activity-needs-strong-${r.key}`}>
                  needs strong
                </span>
              )}
            </label>
            {ACTIVITY[r.key] && <span className="help">{ACTIVITY[r.key].covers}</span>}
            <select
              id={selectId}
              data-testid={selectId}
              className="mono"
              value={chosen}
              onChange={(e) => assign(r.key, e.target.value)}
            >
              {r.optional && follows ? (
                <option value="">
                  follows {activityLabel(follows)}
                  {effectiveEntry(follows, draft.models) ? ` → ${effectiveEntry(follows, draft.models)}` : ""}
                </option>
              ) : (
                chosen === "" && <option value="">— pick a model —</option>
              )}
              {!declared && <option value={chosen}>{chosen} · not in the catalog</option>}
              {names.map((n) => (
                <option key={n} value={n}>
                  {optionLabel(n)}
                </option>
              ))}
            </select>
            {chosen === "" && follows && (
              <span className="help" data-testid={`agent-activity-follows-${r.key}`}>
                follows {activityLabel(follows)} → <span className="mono">{effective ?? "nothing yet"}</span>
              </span>
            )}
            {!r.optional && chosen === "" && (
              <span className="help" style={{ color: "var(--bad)" }} data-testid={`agent-activity-required-${r.key}`}>
                required — every other activity falls back to it
              </span>
            )}
            {!declared && (
              <span className="help" style={{ color: "var(--bad)" }} data-testid={`agent-activity-undeclared-${r.key}`}>
                names an entry the catalog does not declare
              </span>
            )}
            {mismatch && (
              <span className="help" style={{ color: "var(--run)" }} data-testid={`agent-activity-mismatch-${r.key}`}>
                ▲ a fast model on an activity that needs a strong one
              </span>
            )}
          </div>
        );
      })}
    </>
  );
}
