"use client";

import type { ModelPriceList, StudioAgent } from "@/lib/configApi";
import {
  activityLabel,
  orphanOverrides,
  priceLine,
  resolvePrice,
  rolesUsing,
  SOURCE_LABEL,
  windowLabel,
  withOverride,
} from "./modelCatalog";
import { useT } from "@/hooks/useT";


// 2026-09-30-62bab: the Models tab — one card per catalog entry, each showing what the
// resolver would charge for it and which activities name it. A card opens the editor.

export function AgentModelsTab({
  draft,
  onChange,
  prices,
  pricesFailed,
  onAdd,
  onEdit,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
  prices: ModelPriceList | null;
  pricesFailed: boolean;
  onAdd: () => void;
  onEdit: (name: string) => void;
}) {
  const t = useT();
  const entries = Object.entries(draft.catalog ?? {});
  const list = prices?.models ?? null;
  const unpriced = prices
    ? entries.filter(([, e]) => resolvePrice(e.model, draft.pricing, list).source === "none").map(([n]) => n)
    : [];
  const orphans = orphanOverrides(draft);

  return (
    <>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
        <span className="help" data-testid="agent-models-source">
          {prices
            ? <>{t("config.models.source")} <span className="mono">{fetchedLabel(prices.fetchedAt)}</span></>
            : pricesFailed
            ? t("config.models.pricesFailed")
            : t("config.models.pricesLoading")}
        </span>
        <button type="button" className="btn primary" data-testid="agent-model-add" onClick={onAdd}>
          {t("config.models.add")}
        </button>
      </div>

      {unpriced.length > 0 && (
        <div className="integrity warn" role="status" data-testid="agent-models-unpriced">
          <span className="ii" aria-hidden="true">▲</span>
          <span>
            <b className="mono">{unpriced.join(", ")}</b>{" "}
            {t("config.models.unpriced", { count: unpriced.length })}
          </span>
        </div>
      )}

      {entries.length === 0 ? (
        <span className="help" data-testid="agent-models-empty">
          {t("config.models.empty")}
        </span>
      ) : (
        <div className="tpl-rows">
          {entries.map(([name, entry]) => {
            const price = resolvePrice(entry.model, draft.pricing, list);
            const used = rolesUsing(name, draft.models).map((role) => activityLabel(t, role));
            const conn = [entry.providerType, entry.deployment, windowLabel(t, entry.contextWindowTokens)]
              .filter(Boolean)
              .join(" · ");
            return (
              <button
                key={name}
                type="button"
                className="tpl-row"
                data-testid={`agent-model-card-${name}`}
                aria-label={t("config.models.editAria", { name })}
                onClick={() => onEdit(name)}
                style={{ textAlign: "left", font: "inherit", cursor: "pointer", color: "inherit" }}
              >
                <span className="tpl-sum">
                  <span className="tpl-ctx" style={{ flex: 1, fontWeight: 600 }}>{name}</span>
                  {entry.tier && (
                    <span className={`tier-pill ${entry.tier}`} data-testid={`agent-model-tier-${name}`}>
                      {entry.tier}
                    </span>
                  )}
                  {prices && price.source === "none" && <span className="ec-mark warn">{t("config.models.unpricedMark")}</span>}
                </span>
                <span className="help">
                  <span className="mono">{entry.model || t("config.models.noModelId")}</span>
                  {conn ? ` · ${conn}` : ""}
                </span>
                {prices && (
                  <span className="help" data-testid={`agent-model-price-${name}`}>
                    <span className="mono">{priceLine(price)}</span> ·{" "}
                    <span data-testid={`agent-model-source-${name}`}>{t(SOURCE_LABEL[price.source])}</span>
                  </span>
                )}
                <span className="help" data-testid={`agent-model-used-${name}`}>
                  {used.length > 0 ? t("config.models.usedBy", { activities: used.join(", ") }) : t("config.models.notAssigned")}
                </span>
              </button>
            );
          })}
        </div>
      )}

      {orphans.length > 0 && (
        <div className="field" data-testid="agent-orphan-overrides">
          <label>
            {t("config.models.orphans.title")}
            <span className="help">{t("config.models.orphans.help")}</span>
          </label>
          <div className="picks">
            {orphans.map((model) => (
              <button
                key={model}
                type="button"
                className="pick"
                data-testid={`agent-orphan-override-remove-${model}`}
                aria-label={t("config.models.orphans.removeAria", { model })}
                onClick={() => onChange({ ...draft, pricing: withOverride(draft.pricing, model, null) })}
              >
                {model} ✕
              </button>
            ))}
          </div>
        </div>
      )}
    </>
  );
}

function fetchedLabel(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toISOString().slice(0, 10);
}
