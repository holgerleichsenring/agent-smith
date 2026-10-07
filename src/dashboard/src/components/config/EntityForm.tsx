"use client";

import type {
  ConfigCapabilities,
  ConfigEntityKind,
  ConfigFinding,
  InheritedSandboxProjection,
  StudioAgent,
  StudioConnection,
  StudioEntity,
  StudioDesignSource,
  StudioMcpServer,
  StudioProject,
  StudioRepo,
  StudioTracker,
} from "@/lib/configApi";
import { ENTITY_SINGULAR } from "./entities";
import { TextField, SelectField, RefSelect } from "./formFields";
import { CapabilityFieldInputs, pruneToType } from "./capabilityFields";
import { AgentForm } from "./AgentForm";
import { DraftCheckPanel } from "./DraftCheckPanel";
import { ProjectForm } from "./ProjectForm";
import { TrackerForm } from "./TrackerForm";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the create/edit form body, dispatched by entity kind. The `id` is
// editable only when new (it is the primary key). Every reference field is a
// RefSelect/MultiRefSelect bound to a catalog list — the project form is the
// relational heart and gets its own five-tab file (2026-09-16-74a2). The secret form is
// deliberately id-only with a redaction bar: no value input exists anywhere.
// 2026-10-06-cea8: the tracker form is tabbed in its own file, like the project and agent.
// p0345c: tracker/connection/agent forms are CAPABILITIES-driven — type and
// provider are dropdowns from the backend descriptor, and the field set below
// a type renders from that type's declared fields. No hardcoded type knowledge.

const DESIGN_VENDORS = ["figma"];

export function EntityForm({
  kind,
  draft,
  onChange,
  catalog,
  capabilities,
  inheritedSandbox = null,
  isNew,
  findings = [],
}: {
  kind: ConfigEntityKind;
  draft: StudioEntity;
  onChange: (next: StudioEntity) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
  /** 2026-09-22-6968: the project form's sandbox tab shows what each control inherits. */
  inheritedSandbox?: InheritedSandboxProjection | null;
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

  // 2026-10-02-140d: a tracker's and a connection's auth secret is a descriptor field of kind
  // secret, rendered where the descriptor puts it; no fixed picker is added beside it.
  const secretNames = catalog.secrets.map((s) => s.id);

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
    case "trackers":
      return (
        <TrackerForm
          draft={draft as StudioTracker}
          onChange={onChange}
          capabilities={capabilities}
          findings={findings}
          secrets={secretNames}
          idField={idField}
        />
      );
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
              secrets={secretNames}
            />
          )}
          <DraftCheckPanel kind="connections" draft={c} />
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
          inheritedSandbox={inheritedSandbox}
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
    case "design-sources": {
      // 2026-10-01-7f7aa: the vendor list is closed (figma), and the token is a secret
      // picked by NAME — no field here can hold a value.
      const d = draft as StudioDesignSource;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <SelectField
            label="vendor"
            value={d.vendor}
            options={DESIGN_VENDORS}
            required
            testId="form-field-vendor"
            onChange={(v) => onChange({ ...d, vendor: v })}
          />
          <RefSelect
            label="auth secret"
            value={d.authSecret}
            options={catalog.secrets}
            testId="form-ref-authSecret"
            onChange={(v) => onChange({ ...d, authSecret: v })}
          />
          <TextField
            label="display name"
            value={d.displayName ?? ""}
            testId="form-field-displayName"
            onChange={(v) => onChange({ ...d, displayName: v || null })}
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
