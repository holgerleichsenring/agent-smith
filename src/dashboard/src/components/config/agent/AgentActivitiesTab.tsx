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
import { useT } from "@/hooks/useT";


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
  const t = useT();
  const catalog = draft.catalog ?? {};
  const names = Object.keys(catalog);
  const list = prices?.models ?? null;
  const optionLabel = (name: string) => {
    const e = catalog[name];
    const price = prices ? ` · ${shortPrice(t, resolvePrice(e.model, draft.pricing, list))}` : "";
    return `${name} · ${e.tier ?? t("config.activities.noTier")}${price}`;
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
        {t("config.activities.intro")}{" "}
        <b>{t("config.activities.needsStrong")}</b> {t("config.activities.needsStrongMeans")}
      </span>
      {names.length === 0 && (
        <div className="integrity warn" role="status" data-testid="agent-activities-empty">
          <span className="ii" aria-hidden="true">▲</span>
          <span>{t("config.activities.empty")}</span>
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
                {activityLabel(t, r.key)} <span className="mono help">{r.key}</span>
              </span>
              {r.needsStrong && (
                <span className="tier-pill strong" data-testid={`agent-activity-needs-strong-${r.key}`}>
                  {t("config.activities.needsStrong")}
                </span>
              )}
            </label>
            {ACTIVITY[r.key] && <span className="help">{t(ACTIVITY[r.key].covers)}</span>}
            <select
              id={selectId}
              data-testid={selectId}
              className="mono"
              value={chosen}
              onChange={(e) => assign(r.key, e.target.value)}
            >
              {r.optional && follows ? (
                <option value="">
                  {t("config.activities.follows", { activity: activityLabel(t, follows) })}
                  {effectiveEntry(follows, draft.models) ? ` → ${effectiveEntry(follows, draft.models)}` : ""}
                </option>
              ) : (
                chosen === "" && <option value="">{t("config.activities.pickModel")}</option>
              )}
              {!declared && <option value={chosen}>{t("config.activities.notInCatalog", { entry: chosen })}</option>}
              {names.map((n) => (
                <option key={n} value={n}>
                  {optionLabel(n)}
                </option>
              ))}
            </select>
            {chosen === "" && follows && (
              <span className="help" data-testid={`agent-activity-follows-${r.key}`}>
                {t("config.activities.follows", { activity: activityLabel(t, follows) })} →{" "}
                <span className="mono">{effective ?? t("config.activities.nothingYet")}</span>
              </span>
            )}
            {!r.optional && chosen === "" && (
              <span className="help" style={{ color: "var(--bad)" }} data-testid={`agent-activity-required-${r.key}`}>
                {t("config.activities.required")}
              </span>
            )}
            {!declared && (
              <span className="help" style={{ color: "var(--bad)" }} data-testid={`agent-activity-undeclared-${r.key}`}>
                {t("config.activities.undeclared")}
              </span>
            )}
            {mismatch && (
              <span className="help" style={{ color: "var(--run)" }} data-testid={`agent-activity-mismatch-${r.key}`}>
                {t("config.activities.mismatch")}
              </span>
            )}
          </div>
        );
      })}
    </>
  );
}
