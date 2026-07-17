"use client";

import type {
  ConfigCapabilities,
  ConfigEntityKind,
  StudioAgent,
  StudioConnection,
  StudioEntity,
  StudioMcpServer,
  StudioProject,
  StudioRepo,
  StudioTracker,
} from "@/lib/configApi";
import { ENTITY_SINGULAR } from "./entities";
import {
  TextField,
  SelectField,
  NumberField,
  CheckField,
  RefSelect,
  MultiRefSelect,
  ListField,
} from "./formFields";
import { CapabilityFieldInputs, pruneToType } from "./capabilityFields";
import { AgentForm } from "./AgentForm";
import { RepoPicker } from "./RepoPicker";
import { ProjectWiring } from "./ProjectWiring";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the create/edit form body, dispatched by entity kind. The `id` is
// editable only when new (it is the primary key). Every reference field is a
// RefSelect/MultiRefSelect bound to a catalog list — the project form is the
// relational heart and also renders the live wiring preview. The secret form is
// deliberately id-only with a redaction bar: no value input exists anywhere.
// p0345c: tracker/connection/agent forms are CAPABILITIES-driven — type and
// provider are dropdowns from the backend descriptor, and the field set below
// a type renders from that type's declared fields. No hardcoded type knowledge.

// Per-strategy value hints for the project resolution block. The STRATEGY LIST
// itself comes from capabilities; these are only human placeholders/help for
// the strategies the product ships (unknown strategies fall back to a generic
// hint, they are still selectable).
const RESOLUTION_HINTS: Record<string, { placeholder: string; help: string }> = {
  tag: { placeholder: "e.g. Rheview", help: "ticket tag/label that routes to this project" },
  area_path: { placeholder: "e.g. Product\\Team\\Component", help: "the ticket's area path" },
  repo: { placeholder: "e.g. Sample.Api", help: "a repo name mentioned on the ticket" },
  to_address: { placeholder: "e.g. team@example.com", help: "the inbound email address" },
};

export function EntityForm({
  kind,
  draft,
  onChange,
  catalog,
  capabilities,
  isNew,
}: {
  kind: ConfigEntityKind;
  draft: StudioEntity;
  onChange: (next: StudioEntity) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
  isNew: boolean;
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
    case "projects": {
      const p = draft as StudioProject;
      // p0345b: a project's repo refs come in two forms — plain catalog ids
      // (toggled from the repos catalog) and connection-scoped discovery refs
      // "conn/RepoName" (managed by the RepoPicker). Both live in the one
      // `repos` array; the split here is presentational only.
      const plainRefs = p.repos.filter((r) => !r.includes("/"));
      const connRefs = p.repos.filter((r) => r.includes("/"));
      const hint = p.resolution ? RESOLUTION_HINTS[p.resolution.strategy] : undefined;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <RefSelect
            label="agent"
            value={p.agent}
            options={catalog.agents}
            testId="form-ref-agent"
            onChange={(v) => onChange({ ...p, agent: v })}
          />
          <RefSelect
            label="tracker"
            value={p.tracker}
            options={catalog.trackers}
            testId="form-ref-tracker"
            onChange={(v) => onChange({ ...p, tracker: v })}
          />
          <MultiRefSelect
            label="repos"
            values={plainRefs}
            options={catalog.repos}
            testId="form-ref-repos"
            onChange={(v) => onChange({ ...p, repos: [...v, ...connRefs] })}
          />
          <RepoPicker
            label="connection-scoped repos"
            values={connRefs}
            connections={catalog.connections}
            testId="form-connref"
            onChange={(v) => onChange({ ...p, repos: [...plainRefs, ...v] })}
          />
          <SelectField
            label="pipeline"
            value={p.pipeline}
            options={capabilities?.pipelines ?? []}
            help={capabilities ? "the pipeline a triggered ticket runs" : "capabilities unavailable"}
            testId="form-field-pipeline"
            onChange={(v) => onChange({ ...p, pipeline: v })}
          />
          <ListField
            label="pipelines (comma separated)"
            values={p.pipelines}
            testId="form-field-pipelines"
            placeholder="feature-implementation, api-scan"
            onChange={(v) => onChange({ ...p, pipelines: v })}
          />
          <SelectField
            label="resolution strategy"
            value={p.resolution?.strategy ?? ""}
            options={capabilities?.resolutionStrategies ?? []}
            placeholder="— none —"
            help="how a ticket resolves to this project"
            testId="form-field-resolution-strategy"
            onChange={(v) =>
              onChange({ ...p, resolution: v ? { strategy: v, value: p.resolution?.value ?? "" } : null })
            }
          />
          {p.resolution && (
            <TextField
              label="resolution value"
              value={p.resolution.value}
              mono
              placeholder={hint?.placeholder ?? "match value for this strategy"}
              help={hint?.help ?? `value the ${p.resolution.strategy} strategy matches on`}
              testId="form-field-resolution-value"
              onChange={(v) => onChange({ ...p, resolution: { ...p.resolution!, value: v } })}
            />
          )}
          <ProjectWiring project={p} catalog={catalog} />
        </div>
      );
    }
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
