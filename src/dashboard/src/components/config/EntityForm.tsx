"use client";

import type {
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
import { TextField, RefSelect, MultiRefSelect, ConnRefField, ListField } from "./formFields";
import { ProjectWiring } from "./ProjectWiring";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the create/edit form body, dispatched by entity kind. The `id` is
// editable only when new (it is the primary key). Every reference field is a
// RefSelect/MultiRefSelect bound to a catalog list — the project form is the
// relational heart and also renders the live wiring preview. The secret form is
// deliberately id-only with a redaction bar: no value input exists anywhere.

export function EntityForm({
  kind,
  draft,
  onChange,
  catalog,
  isNew,
}: {
  kind: ConfigEntityKind;
  draft: StudioEntity;
  onChange: (next: StudioEntity) => void;
  catalog: ConfigCatalog;
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
          <TextField
            label="provider"
            value={a.provider}
            testId="form-field-provider"
            onChange={(v) => onChange({ ...a, provider: v })}
          />
          <TextField
            label="coding model"
            value={a.models.coding}
            testId="form-field-coding"
            onChange={(v) => onChange({ ...a, models: { ...a.models, coding: v } })}
          />
          <TextField
            label="scan model"
            value={a.models.scan}
            testId="form-field-scan"
            onChange={(v) => onChange({ ...a, models: { ...a.models, scan: v } })}
          />
          <RefSelect
            label="key secret"
            value={a.keySecret}
            options={catalog.secrets}
            testId="form-ref-keySecret"
            onChange={(v) => onChange({ ...a, keySecret: v })}
          />
        </div>
      );
    }
    case "trackers": {
      const t = draft as StudioTracker;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <TextField label="type" value={t.type} testId="form-field-type" onChange={(v) => onChange({ ...t, type: v })} />
          <TextField label="org" value={t.org} testId="form-field-org" onChange={(v) => onChange({ ...t, org: v })} />
          <TextField label="project" value={t.project} testId="form-field-project" onChange={(v) => onChange({ ...t, project: v })} />
          <RefSelect
            label="auth secret"
            value={t.authSecret}
            options={catalog.secrets}
            testId="form-ref-authSecret"
            onChange={(v) => onChange({ ...t, authSecret: v })}
          />
        </div>
      );
    }
    case "connections": {
      const c = draft as StudioConnection;
      return (
        <div className="flex flex-col gap-4">
          {idField}
          <TextField
            label="type"
            value={c.type}
            testId="form-field-type"
            placeholder="e.g. azure-devops"
            onChange={(v) => onChange({ ...c, type: v })}
          />
          <TextField
            label="organization"
            value={c.organization}
            testId="form-field-organization"
            onChange={(v) => onChange({ ...c, organization: v })}
          />
          <TextField
            label="project"
            value={c.project}
            testId="form-field-project"
            onChange={(v) => onChange({ ...c, project: v })}
          />
          <TextField
            label="default branch"
            value={c.defaultBranch}
            testId="form-field-defaultBranch"
            onChange={(v) => onChange({ ...c, defaultBranch: v })}
          />
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
      // "conn/RepoName" (managed by ConnRefField). Both live in the one
      // `repos` array; the split here is presentational only.
      const plainRefs = p.repos.filter((r) => !r.includes("/"));
      const connRefs = p.repos.filter((r) => r.includes("/"));
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
          <ConnRefField
            label="connection-scoped repos"
            values={connRefs}
            connections={catalog.connections}
            testId="form-connref"
            onChange={(v) => onChange({ ...p, repos: [...plainRefs, ...v] })}
          />
          <TextField
            label="trigger"
            value={p.trigger}
            testId="form-field-trigger"
            placeholder="e.g. ticket:ready"
            onChange={(v) => onChange({ ...p, trigger: v })}
          />
          <ListField
            label="pipelines (comma separated)"
            values={p.pipelines}
            testId="form-field-pipelines"
            placeholder="feature-implementation, api-scan"
            onChange={(v) => onChange({ ...p, pipelines: v })}
          />
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
