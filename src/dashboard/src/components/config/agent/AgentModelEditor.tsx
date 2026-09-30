"use client";

import { useState } from "react";
import type {
  AgentCatalogModel,
  AgentPricingEntry,
  ConfigCapabilities,
  ModelPriceList,
  ModelTier,
  StudioAgent,
} from "@/lib/configApi";
import { cn } from "@/lib/utils";
import { DrawerSection, NumberField, SelectField, TextField } from "../formFields";
import { ModelIdSearch } from "./ModelIdSearch";
import {
  activityLabel,
  findListed,
  overrideKey,
  priceLine,
  resolvePrice,
  rolesUsing,
  SOURCE_LABEL,
  withOverride,
} from "./modelCatalog";

// 2026-09-30-62bab: the add/edit sub-view of the Models tab. It edits a local copy and
// writes it to the agent draft only on "Add to catalog" / "Apply", so Back discards. The
// window from the price list is offered as a one-click value and never filled on its own.

const TIERS: { key: ModelTier | null; label: string }[] = [
  { key: "strong", label: "strong" },
  { key: "fast", label: "fast" },
  { key: null, label: "none" },
];

export function AgentModelEditor({
  draft,
  onChange,
  editing,
  prices,
  capabilities,
  onDone,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
  /** The entry being edited; null adds a new one. */
  editing: string | null;
  prices: ModelPriceList | null;
  capabilities: ConfigCapabilities | null;
  onDone: () => void;
}) {
  const catalog = draft.catalog ?? {};
  const original = editing ? catalog[editing] : undefined;
  const [name, setName] = useState(editing ?? "");
  const [entry, setEntry] = useState<AgentCatalogModel>(original ?? { model: "" });
  const originalOverride = original ? overrideKey(draft.pricing, original.model) : null;
  const [override, setOverride] = useState<AgentPricingEntry | null>(
    originalOverride ? draft.pricing!.models[originalOverride] : null,
  );

  const list = prices?.models ?? null;
  const listed = findListed(list, entry.model);
  const pricingPreview = withOverride(draft.pricing, entry.model, override);
  const preview = resolvePrice(entry.model, pricingPreview, list);
  const users = editing ? rolesUsing(editing, draft.models) : [];

  const trimmed = name.trim();
  const nameTaken = trimmed !== editing && trimmed in catalog;
  const valid = trimmed.length > 0 && !nameTaken && entry.model.trim().length > 0;

  function apply() {
    const nextCatalog: Record<string, AgentCatalogModel> = {};
    for (const [n, e] of Object.entries(catalog)) {
      nextCatalog[n === editing ? trimmed : n] = n === editing ? { ...entry, model: entry.model.trim() } : e;
    }
    if (!editing) nextCatalog[trimmed] = { ...entry, model: entry.model.trim() };
    // A rename carries every role that named the entry.
    const nextModels = Object.fromEntries(
      Object.entries(draft.models).map(([role, n]) => [role, n === editing ? trimmed : n]),
    );
    let pricing = draft.pricing;
    if (original && originalOverride && !sameModel(original.model, entry.model)) {
      const shared = Object.entries(catalog).some(([n, e]) => n !== editing && sameModel(e.model, original.model));
      if (!shared) pricing = withOverride(pricing, original.model, null);
    }
    pricing = override || overrideKey(pricing, entry.model) ? withOverride(pricing, entry.model, override) : pricing;
    onChange({ ...draft, catalog: nextCatalog, models: nextModels, pricing });
    onDone();
  }

  function remove() {
    if (!editing || users.length > 0) return;
    const nextCatalog = { ...catalog };
    delete nextCatalog[editing];
    const shared = Object.values(nextCatalog).some((e) => sameModel(e.model, original!.model));
    const pricing = !shared && originalOverride ? withOverride(draft.pricing, original!.model, null) : draft.pricing;
    onChange({ ...draft, catalog: nextCatalog, pricing });
    onDone();
  }

  return (
    <div className="flex flex-col gap-4" data-testid="agent-model-editor">
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <button
          type="button"
          className="btn"
          aria-label="Back to models"
          data-testid="agent-model-back"
          onClick={onDone}
          style={{ minWidth: 44, minHeight: 38, justifyContent: "center" }}
        >
          ←
        </button>
        <h3 style={{ margin: 0, fontSize: 15, fontWeight: 650, flex: 1 }}>
          <span className="help" style={{ fontSize: 15, fontWeight: 500 }}>Models /</span>{" "}
          {editing ? editing : "Add model"}
        </h3>
      </div>

      <ModelIdSearch
        value={entry.model}
        list={list}
        onChange={(model) => {
          setEntry({ ...entry, model });
          if (!editing && (!name.trim() || name === entry.model)) setName(model);
        }}
      />

      <TextField
        label="deployment"
        help="the Azure deployment name, when it differs from the model id"
        value={entry.deployment ?? ""}
        mono
        testId="agent-model-deployment"
        onChange={(v) => setEntry({ ...entry, deployment: v || null })}
      />

      <div className="tpl-row" data-testid="agent-model-price-box">
        <span className="tpl-sum">
          <span style={{ flex: 1, fontSize: 12, fontWeight: 600 }}>Price &amp; window</span>
          <span
            className={cn("ec-mark", preview.source === "none" && "warn")}
            data-testid="agent-model-preview-source"
          >
            {SOURCE_LABEL[preview.source]}
            {preview.source === "list" && listed ? ` · ${listed.id}` : ""}
          </span>
        </span>
        <span className="help">What the resolver charges per million tokens (in / out / cache read):</span>
        <span className="mono" data-testid="agent-model-preview">{priceLine(preview)}</span>
        {entry.model.trim() && !listed && prices && !override && (
          <span className="help" style={{ color: "var(--run)" }}>
            The price list does not know this model — enter an override or the save is refused.
          </span>
        )}
        <label style={{ display: "flex", alignItems: "center", gap: 8, minHeight: 36, fontSize: 12.5 }}>
          <input
            type="checkbox"
            data-testid="agent-model-override"
            checked={override != null}
            style={{ width: 16, height: 16 }}
            onChange={(e) =>
              setOverride(
                e.target.checked
                  ? {
                      inputPerMillion: listed?.inputPerMillion ?? 0,
                      outputPerMillion: listed?.outputPerMillion ?? 0,
                      cacheReadPerMillion: listed?.cacheReadPerMillion ?? null,
                    }
                  : null,
              )
            }
          />
          Override — this installation pays a different rate
        </label>
        {override && (
          <div style={{ display: "flex", gap: 9 }}>
            <PriceInput label="input / M" testId="agent-model-override-input" value={override.inputPerMillion}
              onChange={(v) => setOverride({ ...override, inputPerMillion: v ?? 0 })} />
            <PriceInput label="output / M" testId="agent-model-override-output" value={override.outputPerMillion}
              onChange={(v) => setOverride({ ...override, outputPerMillion: v ?? 0 })} />
            <PriceInput label="cache read / M" testId="agent-model-override-cacheRead" value={override.cacheReadPerMillion ?? undefined}
              onChange={(v) => setOverride({ ...override, cacheReadPerMillion: v ?? null })} />
          </div>
        )}
      </div>

      <div className="field">
        <NumberField
          label="context window"
          help="input tokens the deployment accepts; compaction derives its threshold from it"
          value={entry.contextWindowTokens ?? undefined}
          testId="agent-model-window"
          onChange={(v) => setEntry({ ...entry, contextWindowTokens: v ?? null })}
        />
        {listed?.contextWindowTokens && listed.contextWindowTokens !== entry.contextWindowTokens && (
          <div className="picks">
            <button
              type="button"
              className="pick"
              data-testid="agent-model-window-from-list"
              onClick={() => setEntry({ ...entry, contextWindowTokens: listed.contextWindowTokens })}
            >
              use list value ({listed.contextWindowTokens.toLocaleString("en-US")})
              <span className="rk">from price list</span>
            </button>
          </div>
        )}
      </div>

      <NumberField
        label="max tokens"
        help="per-call output cap; blank = 8192"
        value={entry.maxTokens ?? undefined}
        testId="agent-model-maxTokens"
        onChange={(v) => setEntry({ ...entry, maxTokens: v ?? null })}
      />

      <div className="field">
        <label>
          tier
          <span className="help">an activity that needs a strong model warns when a fast one is picked</span>
        </label>
        <div className="picks" role="radiogroup" aria-label="tier">
          {TIERS.map((t) => {
            const on = (entry.tier ?? null) === t.key;
            return (
              <button
                key={t.label}
                type="button"
                role="radio"
                aria-checked={on}
                className={cn("pick", on && "on")}
                data-testid={`agent-model-tier-pick-${t.label}`}
                onClick={() => setEntry({ ...entry, tier: t.key })}
              >
                {t.label}
              </button>
            );
          })}
        </div>
      </div>

      <TextField
        label="name in the catalog"
        help={nameTaken ? "another entry already has this name" : "what an activity picks"}
        value={name}
        mono
        testId="agent-model-name"
        onChange={setName}
      />

      <DrawerSection
        title="Different provider or endpoint"
        summary={entry.providerType || entry.endpoint ? "set" : "the agent's own"}
        testId="agent-model-connection"
      >
        <SelectField
          label="provider"
          value={entry.providerType ?? ""}
          options={capabilities?.agentProviders ?? []}
          placeholder="— the agent's provider —"
          testId="agent-model-providerType"
          onChange={(v) => setEntry({ ...entry, providerType: v || null })}
        />
        <TextField
          label="endpoint"
          help="blank uses the agent's"
          value={entry.endpoint ?? ""}
          mono
          placeholder="https://…"
          testId="agent-model-endpoint"
          onChange={(v) => setEntry({ ...entry, endpoint: v || null })}
        />
      </DrawerSection>

      <div style={{ display: "flex", gap: 10, alignItems: "center", flexWrap: "wrap" }}>
        {editing && (
          <button
            type="button"
            className="btn"
            data-testid="agent-model-remove"
            disabled={users.length > 0}
            onClick={remove}
            style={{ color: "var(--bad)" }}
          >
            Remove from catalog
          </button>
        )}
        <button
          type="button"
          className="btn primary"
          data-testid="agent-model-apply"
          disabled={!valid}
          onClick={apply}
          style={{ marginLeft: "auto" }}
        >
          {editing ? "Apply" : "Add to catalog"}
        </button>
      </div>
      {users.length > 0 && (
        <span className="help" data-testid="agent-model-remove-refused">
          Cannot remove: {users.map(activityLabel).join(", ")} {users.length === 1 ? "uses" : "use"} this
          model. Pick another model for {users.length === 1 ? "it" : "them"} on the Activities tab first.
        </span>
      )}
      {!valid && (
        <span className="help" data-testid="agent-model-invalid">
          {entry.model.trim() ? (nameTaken ? "Pick a name no other entry has." : "Give the entry a name.") : "Pick the model it runs."}
        </span>
      )}
    </div>
  );
}

function sameModel(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}

function PriceInput({
  label,
  value,
  onChange,
  testId,
}: {
  label: string;
  value: number | undefined;
  onChange: (v: number | undefined) => void;
  testId: string;
}) {
  return (
    <div style={{ flex: 1 }}>
      <NumberField label={label} value={value} onChange={onChange} testId={testId} />
    </div>
  );
}
