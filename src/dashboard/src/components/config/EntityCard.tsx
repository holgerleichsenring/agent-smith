"use client";

import type { ConfigEntityKind, StudioEntity } from "@/lib/configApi";
import type {
  StudioAgent,
  StudioConnection,
  StudioMcpServer,
  StudioProject,
  StudioRepo,
  StudioSecret,
  StudioTracker,
} from "@/lib/configApi";
import { ENTITY_BADGE, ENTITY_ICON } from "./entities";
import { FieldBlock, WiringChip } from "./primitives";
import type { ConfigCatalog } from "./useConfigCatalog";
import { resolves, resolveRepoRef } from "./integrity";

// p0345/p0343c (pixel identity): one entity card in the config-studio.html
// .ecard DOM — .ec-top (icon block, mono id, sub line, type badge + "edit ›"),
// .fields field-block strips, and for projects the .wire row with the green
// project node between the agent → ← tracker chips. Clicking anywhere opens
// the edit drawer. Reference wiring resolves against the live catalog —
// dangling refs go rose via data-resolved.

export function EntityCard({
  kind,
  entity,
  catalog,
  onEdit,
}: {
  kind: ConfigEntityKind;
  entity: StudioEntity;
  catalog: ConfigCatalog;
  onEdit: () => void;
}) {
  return (
    <div
      data-testid={`config-card-${kind}-${entity.id}`}
      className="ecard"
      role="button"
      tabIndex={0}
      onClick={onEdit}
      onKeyDown={(e) => {
        if (e.key === "Enter") onEdit();
      }}
    >
      <div className="ec-top">
        <div className="ec-ic">{ENTITY_ICON[kind]}</div>
        <div>
          <div className="ec-name">{entity.id}</div>
          <SubLine kind={kind} entity={entity} catalog={catalog} />
        </div>
        <div className="ec-right">
          <span className="tybadge" data-testid={`config-card-badge-${entity.id}`}>
            {typeBadge(kind, entity)}
          </span>
          <button
            type="button"
            className="edit-hint"
            data-testid={`config-card-edit-${entity.id}`}
            onClick={(e) => {
              e.stopPropagation();
              onEdit();
            }}
          >
            edit ›
          </button>
        </div>
      </div>
      <CardBody kind={kind} entity={entity} catalog={catalog} />
    </div>
  );
}

function typeBadge(kind: ConfigEntityKind, entity: StudioEntity): string {
  switch (kind) {
    case "agents":
      return (entity as StudioAgent).provider || ENTITY_BADGE.agents;
    case "trackers":
      return (entity as StudioTracker).type || ENTITY_BADGE.trackers;
    case "connections":
      return (entity as StudioConnection).type || ENTITY_BADGE.connections;
    case "repos": {
      const r = entity as StudioRepo;
      return r.branch ? `branch ${r.branch}` : ENTITY_BADGE.repos;
    }
    case "projects": {
      const p = entity as StudioProject;
      return p.pipelines.length > 0 ? p.pipelines.join(" · ") : ENTITY_BADGE.projects;
    }
    case "mcp-servers":
      return (entity as StudioMcpServer).transport || ENTITY_BADGE["mcp-servers"];
    case "secrets":
      return "env-name";
  }
}

function SubLine({
  kind,
  entity,
  catalog,
}: {
  kind: ConfigEntityKind;
  entity: StudioEntity;
  catalog: ConfigCatalog;
}) {
  switch (kind) {
    case "agents": {
      const a = entity as StudioAgent;
      return <div className="ec-sub">{a.keySecret ? `secret ${a.keySecret}` : "no key secret"}</div>;
    }
    case "trackers": {
      const t = entity as StudioTracker;
      return <div className="ec-sub">auth {t.authSecret || "—"}</div>;
    }
    case "connections": {
      const c = entity as StudioConnection;
      return <div className="ec-sub">auth {c.authSecret || "—"}</div>;
    }
    case "repos": {
      const r = entity as StudioRepo;
      return <div className="ec-sub">{r.name || "—"}</div>;
    }
    case "projects": {
      const p = entity as StudioProject;
      return (
        <div className="ec-sub">
          {p.pipelines.length} {p.pipelines.length === 1 ? "pipeline" : "pipelines"}
          {p.trigger ? <> · trigger {p.trigger}</> : null}
        </div>
      );
    }
    case "mcp-servers": {
      const m = entity as StudioMcpServer;
      return <div className="ec-sub">{m.url || "—"}</div>;
    }
    case "secrets": {
      const s = entity as StudioSecret;
      const used = secretUsers(catalog, s.id);
      return (
        <div className="ec-sub" data-testid={`config-secret-redaction-${s.id}`}>
          {used.length > 0 ? `used by ${used.join(", ")}` : "not referenced yet"} · value resolved
          from runtime, never stored here
        </div>
      );
    }
  }
}

// Which catalog entries reference this secret's env-name — real, client-derived
// from the same catalog the pickers use.
function secretUsers(catalog: ConfigCatalog, id: string): string[] {
  const used: string[] = [];
  for (const a of catalog.agents) if (a.keySecret === id) used.push(a.id);
  for (const t of catalog.trackers) if (t.authSecret === id) used.push(t.id);
  for (const c of catalog.connections) if (c.authSecret === id) used.push(c.id);
  for (const m of catalog["mcp-servers"]) if (m.authSecret === id) used.push(m.id);
  return used;
}

function CardBody({
  kind,
  entity,
  catalog,
}: {
  kind: ConfigEntityKind;
  entity: StudioEntity;
  catalog: ConfigCatalog;
}) {
  switch (kind) {
    case "agents": {
      const a = entity as StudioAgent;
      // p0343b: list the model roles ACTUALLY present on the entry.
      const roles = Object.entries(a.models).filter(([, model]) => model);
      return (
        <div className="fields">
          {roles.map(([role, model], i) => (
            <div className="f" key={role} data-testid={`config-card-model-${a.id}-${role}`}>
              <span className="fl">{role} model</span>
              <span className={i === 0 ? "fv link" : "fv"}>{model}</span>
            </div>
          ))}
          <div className="f" data-testid={`config-card-key-${a.id}`}
            data-resolved={!a.keySecret || resolves(catalog, "secrets", a.keySecret) ? "true" : "false"}
          >
            <span className="fl">key secret</span>
            <span
              className="fv"
              style={
                a.keySecret && !resolves(catalog, "secrets", a.keySecret)
                  ? { color: "var(--bad)" }
                  : undefined
              }
            >
              {a.keySecret || "—"}
            </span>
          </div>
        </div>
      );
    }
    case "trackers": {
      const t = entity as StudioTracker;
      return (
        <div className="fields">
          <FieldBlock label="Organisation">{t.org || "—"}</FieldBlock>
          <FieldBlock label="Project">{t.project || "—"}</FieldBlock>
          <div className="f" data-resolved={resolves(catalog, "secrets", t.authSecret) ? "true" : "false"}>
            <span className="fl">Auth</span>
            <span
              className="fv"
              style={!resolves(catalog, "secrets", t.authSecret) ? { color: "var(--bad)" } : undefined}
            >
              {t.authSecret || "—"}
            </span>
          </div>
        </div>
      );
    }
    case "connections": {
      const c = entity as StudioConnection;
      return (
        <div className="fields">
          <FieldBlock label="Organisation">{c.organization || "—"}</FieldBlock>
          <FieldBlock label="Project">{c.project || "—"}</FieldBlock>
          <FieldBlock label="Default branch">{c.defaultBranch || "—"}</FieldBlock>
          <div
            className="f"
            data-testid={`config-card-connection-auth-${c.id}`}
            data-resolved={resolves(catalog, "secrets", c.authSecret) ? "true" : "false"}
          >
            <span className="fl">Auth</span>
            <span
              className="fv"
              style={!resolves(catalog, "secrets", c.authSecret) ? { color: "var(--bad)" } : undefined}
            >
              {c.authSecret || "—"}
            </span>
          </div>
        </div>
      );
    }
    case "repos":
      return null; // the mock repo card is ec-top only (name in the sub line)
    case "projects": {
      const p = entity as StudioProject;
      return (
        <div className="wire" data-testid={`config-card-wiring-${p.id}`}>
          <span className="wlbl">wires</span>
          <WiringChip
            label="agent"
            kind="agent"
            value={p.agent}
            resolved={resolves(catalog, "agents", p.agent)}
            testId={`config-card-agent-${p.id}`}
          />
          <span className="warr">→</span>
          {/* p0343b: the project itself is the GREEN center of the wires row. */}
          <span
            data-testid={`config-card-project-chip-${p.id}`}
            className="pw-node proj"
            style={{ padding: "4px 10px", fontSize: "11.5px" }}
          >
            {p.id}
          </span>
          <span className="warr">←</span>
          <WiringChip
            label="tracker"
            kind="tracker"
            value={p.tracker}
            resolved={resolves(catalog, "trackers", p.tracker)}
            testId={`config-card-tracker-${p.id}`}
          />
          <span className="warr">·</span>
          {p.repos.length === 0 && <span className="wlbl">no repos</span>}
          {p.repos.map((repoId) => {
            // p0345b: conn-scoped discovery refs ("conn/Name") resolve against
            // the connections catalog; plain refs against repos.
            const result = resolveRepoRef(catalog, repoId);
            return (
              <WiringChip
                key={repoId}
                label={repoId.includes("/") ? "conn repo" : "repo"}
                kind="repo"
                value={repoId}
                resolved={result.ok}
                testId={`config-card-repo-${p.id}-${repoId}`}
              />
            );
          })}
        </div>
      );
    }
    case "mcp-servers": {
      const m = entity as StudioMcpServer;
      return (
        <div className="fields">
          <FieldBlock label="Transport">{m.transport || "—"}</FieldBlock>
          <div className="f" data-resolved={resolves(catalog, "secrets", m.authSecret) ? "true" : "false"}>
            <span className="fl">Auth</span>
            <span
              className="fv"
              style={!resolves(catalog, "secrets", m.authSecret) ? { color: "var(--bad)" } : undefined}
            >
              {m.authSecret || "—"}
            </span>
          </div>
        </div>
      );
    }
    case "secrets":
      return null; // the mock secret card is ec-top only (usage in the sub line)
  }
}
