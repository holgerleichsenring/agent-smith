"use client";

import { useState } from "react";
import type {
  AgentModelEntry,
  AgentPricingEntry,
  ConfigCapabilities,
  StudioAgent,
} from "@/lib/configApi";
import type { ConfigCatalog } from "./useConfigCatalog";
import {
  TextField,
  SelectField,
  NumberField,
  CheckField,
  RefSelect,
  DrawerSection,
} from "./formFields";

// p0345c: the FULL agent surface as a sectioned drawer — Provider & endpoint /
// Models per role / Pricing / Cache / Compaction / Retry. The provider dropdown
// comes from the capabilities descriptor; the optional sections (pricing,
// cache, compaction, retry) exist on the draft only after the operator adds
// them, so an untouched section is never persisted.

export function AgentForm({
  draft,
  onChange,
  catalog,
  capabilities,
}: {
  draft: StudioAgent;
  onChange: (next: StudioAgent) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
}) {
  const roles = Object.keys(draft.models);
  // p0351: a role's model must be priced — flag the ones that aren't so the rule
  // the backend enforces on save is visible while editing.
  const pricedModels = new Set(Object.keys(draft.pricing?.models ?? {}));
  return (
    <>
      <DrawerSection title="Provider & endpoint" defaultOpen testId="agent-section-provider">
        <SelectField
          label="provider"
          value={draft.provider}
          options={capabilities?.agentProviders ?? []}
          required
          help={capabilities ? undefined : "capabilities unavailable"}
          testId="form-field-provider"
          onChange={(v) => onChange({ ...draft, provider: v })}
        />
        <RefSelect
          label="key secret"
          value={draft.keySecret ?? ""}
          options={catalog.secrets}
          testId="form-ref-keySecret"
          onChange={(v) => onChange({ ...draft, keySecret: v || null })}
        />
        <TextField
          label="endpoint"
          value={draft.endpoint ?? ""}
          mono
          testId="form-field-endpoint"
          placeholder="https://…"
          onChange={(v) => onChange({ ...draft, endpoint: v || undefined })}
        />
        <TextField
          label="api version"
          value={draft.apiVersion ?? ""}
          mono
          testId="form-field-apiVersion"
          onChange={(v) => onChange({ ...draft, apiVersion: v || undefined })}
        />
        <NumberField
          label="network timeout (seconds)"
          value={draft.networkTimeoutSeconds}
          testId="form-field-networkTimeoutSeconds"
          onChange={(v) => onChange({ ...draft, networkTimeoutSeconds: v })}
        />
      </DrawerSection>

      <DrawerSection
        title="Models per role"
        defaultOpen
        summary={roles.length > 0 ? `${roles.length} ${roles.length === 1 ? "role" : "roles"}` : "none yet"}
        testId="agent-section-models"
      >
        {/* p0351: the roles are the fixed TaskType set from capabilities, not a
            free-text add-role box — only these keys route to a model. Optional
            roles (reasoning) are offered as a seed until added. */}
        {(capabilities?.roles ?? roles.map((key) => ({ key, optional: true }))).map((r) => {
          if (!(r.key in draft.models) && r.optional) {
            return (
              <SectionSeed
                key={r.key}
                text={`No ${r.key} model — the backend default applies.`}
                action={`Add ${r.key} role`}
                testId={`agent-add-role-${r.key}`}
                onAdd={() => onChange({ ...draft, models: { ...draft.models, [r.key]: { model: "" } } })}
              />
            );
          }
          const entry = draft.models[r.key] ?? { model: "" };
          return (
            <ModelRoleRow
              key={r.key}
              role={r.key}
              entry={entry}
              unpriced={entry.model.trim() !== "" && !pricedModels.has(entry.model)}
              onChange={(e) => onChange({ ...draft, models: { ...draft.models, [r.key]: e } })}
              onRemove={
                r.optional
                  ? () => {
                      const next = { ...draft.models };
                      delete next[r.key];
                      onChange({ ...draft, models: next });
                    }
                  : undefined
              }
            />
          );
        })}
      </DrawerSection>

      <DrawerSection
        title="Pricing"
        summary={draft.pricing ? `${Object.keys(draft.pricing.models).length} models` : "not set"}
        testId="agent-section-pricing"
      >
        {!draft.pricing ? (
          <SectionSeed
            text="No pricing table — cost rollups fall back to backend defaults."
            action="Add pricing table"
            testId="agent-add-pricing"
            onAdd={() => onChange({ ...draft, pricing: { models: {} } })}
          />
        ) : (
          <>
            {Object.entries(draft.pricing.models).map(([name, entry]) => (
              <PricingRow
                key={name}
                name={name}
                entry={entry}
                onChange={(e) =>
                  onChange({ ...draft, pricing: { models: { ...draft.pricing!.models, [name]: e } } })
                }
                onRemove={() => {
                  const models = { ...draft.pricing!.models };
                  delete models[name];
                  onChange({ ...draft, pricing: Object.keys(models).length > 0 ? { models } : undefined });
                }}
              />
            ))}
            <AddByName
              label="add model pricing"
              placeholder="model name"
              testId="agent-add-pricing-model"
              existing={Object.keys(draft.pricing.models)}
              onAdd={(name) =>
                onChange({
                  ...draft,
                  pricing: {
                    models: { ...draft.pricing!.models, [name]: { inputPerMillion: 0, outputPerMillion: 0 } },
                  },
                })
              }
            />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title="Cache"
        summary={draft.cache ? (draft.cache.isEnabled ? "enabled" : "disabled") : "not set"}
        testId="agent-section-cache"
      >
        {!draft.cache ? (
          <SectionSeed
            text="No cache settings — the backend default applies."
            action="Add cache settings"
            testId="agent-add-cache"
            onAdd={() => onChange({ ...draft, cache: { isEnabled: true, strategy: "" } })}
          />
        ) : (
          <>
            <CheckField
              label="prompt caching"
              value={draft.cache.isEnabled}
              testId="form-field-cache-isEnabled"
              onChange={(v) => onChange({ ...draft, cache: { ...draft.cache!, isEnabled: v } })}
            />
            <TextField
              label="strategy"
              value={draft.cache.strategy}
              testId="form-field-cache-strategy"
              onChange={(v) => onChange({ ...draft, cache: { ...draft.cache!, strategy: v } })}
            />
            <ClearSection testId="agent-clear-cache" onClear={() => onChange({ ...draft, cache: undefined })} />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title="Compaction"
        summary={draft.compaction ? (draft.compaction.isEnabled ? "enabled" : "disabled") : "not set"}
        testId="agent-section-compaction"
      >
        {!draft.compaction ? (
          <SectionSeed
            text="No compaction settings — the backend default applies."
            action="Add compaction settings"
            testId="agent-add-compaction"
            onAdd={() =>
              onChange({
                ...draft,
                compaction: {
                  isEnabled: true,
                  thresholdIterations: 0,
                  maxContextTokens: 0,
                  keepRecentIterations: 0,
                  summaryModel: "",
                },
              })
            }
          />
        ) : (
          <>
            <CheckField
              label="compaction"
              value={draft.compaction.isEnabled}
              testId="form-field-compaction-isEnabled"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, isEnabled: v } })}
            />
            <NumberField
              label="threshold iterations"
              value={draft.compaction.thresholdIterations}
              testId="form-field-compaction-thresholdIterations"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, thresholdIterations: v ?? 0 } })}
            />
            <NumberField
              label="max context tokens"
              value={draft.compaction.maxContextTokens}
              testId="form-field-compaction-maxContextTokens"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, maxContextTokens: v ?? 0 } })}
            />
            <NumberField
              label="keep recent iterations"
              value={draft.compaction.keepRecentIterations}
              testId="form-field-compaction-keepRecentIterations"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, keepRecentIterations: v ?? 0 } })}
            />
            <TextField
              label="summary model"
              value={draft.compaction.summaryModel}
              testId="form-field-compaction-summaryModel"
              onChange={(v) => onChange({ ...draft, compaction: { ...draft.compaction!, summaryModel: v } })}
            />
            <ClearSection testId="agent-clear-compaction" onClear={() => onChange({ ...draft, compaction: undefined })} />
          </>
        )}
      </DrawerSection>

      <DrawerSection
        title="Retry"
        summary={draft.retry ? `${draft.retry.maxRetries} retries` : "not set"}
        testId="agent-section-retry"
      >
        {!draft.retry ? (
          <SectionSeed
            text="No retry policy — the backend default applies."
            action="Add retry policy"
            testId="agent-add-retry"
            onAdd={() =>
              onChange({
                ...draft,
                retry: { maxRetries: 3, initialDelayMs: 1000, backoffMultiplier: 2, maxDelayMs: 30000 },
              })
            }
          />
        ) : (
          <>
            <NumberField
              label="max retries"
              value={draft.retry.maxRetries}
              testId="form-field-retry-maxRetries"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, maxRetries: v ?? 0 } })}
            />
            <NumberField
              label="initial delay (ms)"
              value={draft.retry.initialDelayMs}
              testId="form-field-retry-initialDelayMs"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, initialDelayMs: v ?? 0 } })}
            />
            <NumberField
              label="backoff multiplier"
              value={draft.retry.backoffMultiplier}
              testId="form-field-retry-backoffMultiplier"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, backoffMultiplier: v ?? 0 } })}
            />
            <NumberField
              label="max delay (ms)"
              value={draft.retry.maxDelayMs}
              testId="form-field-retry-maxDelayMs"
              onChange={(v) => onChange({ ...draft, retry: { ...draft.retry!, maxDelayMs: v ?? 0 } })}
            />
            <ClearSection testId="agent-clear-retry" onClear={() => onChange({ ...draft, retry: undefined })} />
          </>
        )}
      </DrawerSection>
    </>
  );
}

function ModelRoleRow({
  role,
  entry,
  unpriced,
  onChange,
  onRemove,
}: {
  role: string;
  entry: AgentModelEntry;
  unpriced?: boolean;
  onChange: (e: AgentModelEntry) => void;
  onRemove?: () => void;
}) {
  return (
    <div className="field" data-testid={`agent-role-${role}`}>
      <label>
        {role}
        {unpriced && (
          <span
            className="help"
            data-testid={`agent-role-unpriced-${role}`}
            style={{ color: "var(--bad)", marginLeft: 8 }}
          >
            no pricing entry
          </span>
        )}
        {onRemove && (
          <button
            type="button"
            className="help"
            data-testid={`agent-role-remove-${role}`}
            onClick={onRemove}
            style={{ background: "none", border: 0, cursor: "pointer", marginLeft: "auto" }}
          >
            remove
          </button>
        )}
      </label>
      <div style={{ display: "flex", gap: 9 }}>
        <div className="field" style={{ flex: 2 }}>
          <label>model</label>
          <input
            type="text"
            className="mono"
            data-testid={`form-field-${role}`}
            value={entry.model}
            onChange={(e) => onChange({ ...entry, model: e.target.value })}
          />
        </div>
        <div className="field" style={{ flex: 2 }}>
          <label>deployment</label>
          <input
            type="text"
            className="mono"
            data-testid={`form-field-${role}-deployment`}
            value={entry.deployment ?? ""}
            onChange={(e) => onChange({ ...entry, deployment: e.target.value || undefined })}
          />
        </div>
        <div className="field" style={{ flex: 1 }}>
          <label>max tokens</label>
          <input
            type="number"
            className="mono"
            data-testid={`form-field-${role}-maxTokens`}
            value={entry.maxTokens ?? ""}
            onChange={(e) =>
              onChange({ ...entry, maxTokens: e.target.value.trim() === "" ? undefined : Number(e.target.value) })
            }
          />
        </div>
      </div>
    </div>
  );
}

function PricingRow({
  name,
  entry,
  onChange,
  onRemove,
}: {
  name: string;
  entry: AgentPricingEntry;
  onChange: (e: AgentPricingEntry) => void;
  onRemove: () => void;
}) {
  const num = (v: string) => (v.trim() === "" ? 0 : Number(v));
  return (
    <div className="field" data-testid={`agent-pricing-${name}`}>
      <label>
        {name}
        <span className="help">$ per million tokens</span>
        <button
          type="button"
          className="help"
          data-testid={`agent-pricing-remove-${name}`}
          onClick={onRemove}
          style={{ background: "none", border: 0, cursor: "pointer", marginLeft: "auto" }}
        >
          remove
        </button>
      </label>
      <div style={{ display: "flex", gap: 9 }}>
        <div className="field" style={{ flex: 1 }}>
          <label>input</label>
          <input
            type="number"
            className="mono"
            data-testid={`form-field-pricing-${name}-input`}
            value={entry.inputPerMillion}
            onChange={(e) => onChange({ ...entry, inputPerMillion: num(e.target.value) })}
          />
        </div>
        <div className="field" style={{ flex: 1 }}>
          <label>output</label>
          <input
            type="number"
            className="mono"
            data-testid={`form-field-pricing-${name}-output`}
            value={entry.outputPerMillion}
            onChange={(e) => onChange({ ...entry, outputPerMillion: num(e.target.value) })}
          />
        </div>
        <div className="field" style={{ flex: 1 }}>
          <label>cache read</label>
          <input
            type="number"
            className="mono"
            data-testid={`form-field-pricing-${name}-cacheRead`}
            value={entry.cacheReadPerMillion ?? ""}
            onChange={(e) =>
              onChange({
                ...entry,
                cacheReadPerMillion: e.target.value.trim() === "" ? undefined : Number(e.target.value),
              })
            }
          />
        </div>
      </div>
    </div>
  );
}

// "add <thing> by name" input+button used for roles and pricing rows.
function AddByName({
  label,
  placeholder,
  existing,
  onAdd,
  testId,
}: {
  label: string;
  placeholder: string;
  existing: string[];
  onAdd: (name: string) => void;
  testId: string;
}) {
  const [name, setName] = useState("");
  const trimmed = name.trim();
  const valid = trimmed.length > 0 && !existing.includes(trimmed);
  return (
    <div style={{ display: "flex", gap: 9, alignItems: "flex-end" }}>
      <div className="field" style={{ flex: 1 }}>
        <label>{label}</label>
        <input
          type="text"
          className="mono"
          data-testid={`${testId}-name`}
          value={name}
          placeholder={placeholder}
          onChange={(e) => setName(e.target.value)}
        />
      </div>
      <button
        type="button"
        className="pick"
        data-testid={testId}
        disabled={!valid}
        onClick={() => {
          onAdd(trimmed);
          setName("");
        }}
        style={!valid ? { opacity: 0.5, cursor: "not-allowed" } : undefined}
      >
        Add
      </button>
    </div>
  );
}

function SectionSeed({
  text,
  action,
  onAdd,
  testId,
}: {
  text: string;
  action: string;
  onAdd: () => void;
  testId: string;
}) {
  return (
    <div className="field">
      <span className="help">{text}</span>
      <div className="picks">
        <button type="button" className="pick" data-testid={testId} onClick={onAdd}>
          {action}
        </button>
      </div>
    </div>
  );
}

function ClearSection({ onClear, testId }: { onClear: () => void; testId: string }) {
  return (
    <div className="picks">
      <button type="button" className="pick" data-testid={testId} onClick={onClear}>
        Remove section (use backend default)
      </button>
    </div>
  );
}
