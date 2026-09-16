"use client";

import type {
  ConfigCapabilities,
  ConfigEntityKind,
  ConfigFinding,
  StudioAgent,
  StudioConnection,
  StudioEntity,
  StudioMcpServer,
  StudioProject,
  StudioRepo,
  StudioTracker,
} from "@/lib/configApi";
import { ENTITY_SINGULAR } from "./entities";
import { TextField, SelectField, NumberField, CheckField, RefSelect } from "./formFields";
import { CapabilityFieldInputs, pruneToType } from "./capabilityFields";
import { AgentForm } from "./AgentForm";
import { ProjectForm } from "./ProjectForm";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the create/edit form body, dispatched by entity kind. The `id` is
// editable only when new (it is the primary key). Every reference field is a
// RefSelect/MultiRefSelect bound to a catalog list — the project form is the
// relational heart and gets its own five-tab file (2026-09-16-74a2). The secret form is
// deliberately id-only with a redaction bar: no value input exists anywhere.
// p0345c: tracker/connection/agent forms are CAPABILITIES-driven — type and
// provider are dropdowns from the backend descriptor, and the field set below
// a type renders from that type's declared fields. No hardcoded type knowledge.

export function EntityForm({
  kind,
  draft,
  onChange,
  catalog,
  capabilities,
  isNew,
  findings = [],
}: {
  kind: ConfigEntityKind;
  draft: StudioEntity;
  onChange: (next: StudioEntity) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
  isNew: boolean;
  /** p0392: what the server says about this unsaved draft, from its own rules. */
  findings?: ConfigFinding[];
}) {
  const idField = (
    <TextField
      label={`${ENTITY_SINGULAR[kind]} id`}
      value={draft.id}
      mono
      disabled={!isNew}
      testId="form-field-id"
      placeholder={kind === "secrets" ? "ENV_VAR_NAME" : "unique-id"}
      onChange={(v) => onChange({ ...draft, id: v })}
    />
  );

  switch (kind) {
    case "agents": {
      const a = draft as StudioAgent;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <AgentForm draft={a} onChange={onChange} catalog={catalog} capabilities={capabilities} />
        </div>
      );
    }
    case "trackers": {
      const t = draft as StudioTracker;
      const descriptor = capabilities?.trackerTypes.find((d) => d.type === t.type) ?? null;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <SelectField
            label="type"
            value={t.type}
            options={capabilities?.trackerTypes.map((d) => d.type) ?? []}
            required
            help={capabilities ? undefined : "capabilities unavailable"}
            testId="form-field-type"
            onChange={(v) => onChange(pruneToType(t, capabilities?.trackerTypes ?? [], v))}
          />
          {descriptor && (
            <CapabilityFieldInputs
              fields={descriptor.fields}
              values={t as unknown as Record<string, unknown>}
              onFieldChange={(key, value) => onChange({ ...t, [key]: value })}
              findings={findings}
            />
          )}
          <RefSelect
            label="auth secret"
            value={t.authSecret}
            options={catalog.secrets}
            testId="form-ref-authSecret"
            onChange={(v) => onChange({ ...t, authSecret: v })}
          />
          <TrackerPollingBlock tracker={t} onChange={onChange} />
        </div>
      );
    }
    case "connections": {
      const c = draft as StudioConnection;
      const descriptor = capabilities?.connectionTypes.find((d) => d.type === c.type) ?? null;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <SelectField
            label="type"
            value={c.type}
            options={capabilities?.connectionTypes.map((d) => d.type) ?? []}
            required
            help={capabilities ? undefined : "capabilities unavailable"}
            testId="form-field-type"
            onChange={(v) => onChange(pruneToType(c, capabilities?.connectionTypes ?? [], v))}
          />
          {descriptor && (
            <CapabilityFieldInputs
              fields={descriptor.fields}
              values={c as unknown as Record<string, unknown>}
              onFieldChange={(key, value) => onChange({ ...c, [key]: value })}
              orgLabel={descriptor.orgLabel}
              findings={findings}
            />
          )}
          <RefSelect
            label="auth secret"
            value={c.authSecret}
            options={catalog.secrets}
            testId="form-ref-authSecret"
            onChange={(v) => onChange({ ...c, authSecret: v })}
          />
        </div>
      );
    }
    case "repos": {
      const r = draft as StudioRepo;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <TextField label="name" value={r.name} testId="form-field-name" onChange={(v) => onChange({ ...r, name: v })} />
          <TextField label="branch" value={r.branch} testId="form-field-branch" onChange={(v) => onChange({ ...r, branch: v })} />
        </div>
      );
    }
    case "projects":
      return (
        <ProjectForm
          project={draft as StudioProject}
          onChange={onChange}
          catalog={catalog}
          capabilities={capabilities}
          findings={findings}
          idField={idField}
        />
      );
    case "mcp-servers": {
      const m = draft as StudioMcpServer;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <TextField label="transport" value={m.transport} testId="form-field-transport" onChange={(v) => onChange({ ...m, transport: v })} />
          <TextField label="url" value={m.url} testId="form-field-url" onChange={(v) => onChange({ ...m, url: v })} />
          <RefSelect
            label="auth secret"
            value={m.authSecret}
            options={catalog.secrets}
            testId="form-ref-authSecret"
            onChange={(v) => onChange({ ...m, authSecret: v })}
          />
        </div>
      );
    }
    case "secrets": {
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <div
            data-testid="secret-redaction-bar"
            className="flex items-center gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-2"
          >
            <span className="dsh-label font-semibold uppercase tracking-wide text-amber-700">
              value redacted
            </span>
            <span className="dsh-body text-amber-700">
              The studio stores the env-NAME only — the value is resolved from the runtime secret store and is never entered or shown here.
            </span>
          </div>
        </div>
      );
    }
  }
}

// p0345c: polling is part of the v2 tracker CONTRACT (not per-type) — an
// optional section: absent until the operator adds it, then enabled/interval/
// jitter are editable; removing it restores the backend default.
function TrackerPollingBlock({
  tracker,
  onChange,
}: {
  tracker: StudioTracker;
  onChange: (next: StudioTracker) => void;
}) {
  if (!tracker.polling) {
    return (
      <div className="field">
        <label>
          polling <span className="help">backend default applies</span>
        </label>
        <div className="picks">
          <button
            type="button"
            className="pick"
            data-testid="form-field-polling-add"
            onClick={() =>
              onChange({ ...tracker, polling: { enabled: true, intervalSeconds: 300, jitterPercent: 10 } })
            }
          >
            Configure polling
          </button>
        </div>
      </div>
    );
  }
  const polling = tracker.polling;
  return (
    <>
      <CheckField
        label="polling"
        value={polling.enabled}
        testId="form-field-polling-enabled"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, enabled: v } })}
      />
      <NumberField
        label="poll interval (seconds)"
        value={polling.intervalSeconds}
        testId="form-field-polling-intervalSeconds"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, intervalSeconds: v ?? 0 } })}
      />
      <NumberField
        label="jitter (%)"
        value={polling.jitterPercent}
        testId="form-field-polling-jitterPercent"
        onChange={(v) => onChange({ ...tracker, polling: { ...polling, jitterPercent: v ?? 0 } })}
      />
      <div className="picks">
        <button
          type="button"
          className="pick"
          data-testid="form-field-polling-remove"
          onClick={() => onChange({ ...tracker, polling: undefined })}
        >
          Remove polling override
        </button>
      </div>
    </>
  );
}
