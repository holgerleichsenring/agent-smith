"use client";

import { useState } from "react";
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
import { FieldBlock } from "./primitives";
import type { ConfigCatalog } from "./useConfigCatalog";
import { resolves, resolveRepoRef, unfinishedTemplates } from "./integrity";
import { ProjectInitAction } from "@/components/system/ProjectInitAction";
import { ProjectGraph } from "./ProjectGraph";

// p0345/p0343c (pixel identity): one entity card in the config-studio.html
// .ecard DOM — .ec-top (icon block, mono id, sub line, type badge + "edit ›"),
// .fields field-block strips. Clicking anywhere opens the edit drawer.
// 2026-09-16-bedc: a project card has NO body. Its wiring is the graph behind its own
// control; the flat chip row that used to sit here was the second copy of it, left in
// place because 942a described an addition where a replacement was meant.

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
  // 2026-09-16-942a: a project card expands into the graph of what it is wired to. The
  // whole card is the edit button, so the disclosure control stops the event — the
  // precedent is the init action already sitting on these cards — and the card's
  // overflow, which is hidden, is lifted while the drawing is out.
  const [expanded, setExpanded] = useState(false);
  return (
    <div
      data-testid={`config-card-${kind}-${entity.id}`}
      className="ecard"
      data-expanded={expanded ? "true" : "false"}
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
          {/* p0489: a project can be asked to initialize itself right here. */}
          {kind === "projects" && <ProjectInitAction project={entity.id} />}
          {kind === "projects" && (
            <button
              type="button"
              className="edit-hint"
              aria-expanded={expanded}
              aria-label={expanded ? `Hide how ${entity.id} is wired` : `Show how ${entity.id} is wired`}
              data-testid={`config-card-graph-toggle-${entity.id}`}
              onClick={(e) => {
                e.stopPropagation();
                setExpanded((x) => !x);
              }}
            >
              {expanded ? "▾ wiring" : "▸ wiring"}
            </button>
          )}
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
      {kind === "projects" && expanded && (
        <div className="pgraph-wrap" onClick={(e) => e.stopPropagation()}>
          <ProjectGraph project={entity as StudioProject} catalog={catalog} />
        </div>
      )}
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
    case "projects":
      return <ProjectSubLine project={entity as StudioProject} catalog={catalog} />;
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
      // p0345c: entries are objects now — show the model, hint the deployment.
      const roles = Object.entries(a.models).filter(([, entry]) => entry?.model);
      return (
        <div className="fields">
          {roles.map(([role, entry], i) => (
            <div className="f" key={role} data-testid={`config-card-model-${a.id}-${role}`}>
              <span className="fl">{role} model</span>
              <span className={i === 0 ? "fv link" : "fv"}>
                {entry.model}
                {entry.deployment ? ` (${entry.deployment})` : ""}
              </span>
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
      // p0345c: tracker fields are per-type — show what the entry carries.
      return (
        <div className="fields">
          <FieldBlock label="Organisation">{t.organization || "—"}</FieldBlock>
          <FieldBlock label="Project">{t.project || "—"}</FieldBlock>
          {t.url && <FieldBlock label="Url">{t.url}</FieldBlock>}
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
    case "projects":
      // 2026-09-16-bedc: the graph behind the card's own control is the wiring. This case
      // drew a second one, a flat chip row, unconditionally — the very shape 942a's graph
      // was built to replace, left beside it because that spec described an addition.
      return null;

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

// p0345c truth-fix: the sub-line names the PIPELINE (once mislabeled "trigger") and, when
// set, the resolution strategy that routes tickets.
// 2026-09-15-9b3e: and what the project is built AFTER. A template was declarable and
// invisible on the card that summarises the project — its own function, because the switch
// above is long enough without a second clause inside one case.
/**
 * 2026-09-16-942a: the facts an operator scans. It said "0 pipelines" for a project that
 * runs perfectly well, because it counted a list that decides nothing about what a ticket
 * runs. It names the default pipeline — what actually answers when nothing routed — how a
 * ticket reaches the project, the repository count, and whether anything is unresolved.
 */
function ProjectSubLine({ project, catalog }: { project: StudioProject; catalog: ConfigCatalog }) {
  const templates = project.templates ?? [];
  const targets = [...new Set(templates.map((t) => t.project).filter(Boolean))];
  const pipeline = project.defaultPipeline || project.pipeline || "none declared";
  const unresolved = unresolvedRefs(project, catalog);
  const unfinished = unfinishedTemplates(project).length;
  return (
    // 2026-09-16-bedc: one MARK per fact. It was four facts of different kinds separated by
    // the same character, so none could be found without reading all of them. The outer class
    // stays — it is shared by all seven kinds and a secret's summary is a sentence — and the
    // marks sit under one of their own.
    <div className="ec-sub ec-marks">
      <span className="ec-mark" data-testid={`config-project-pipeline-${project.id}`}>
        {pipeline === "none declared" ? "no default pipeline" : `default · ${pipeline}`}
      </span>
      {project.resolution ? (
        <span className="ec-mark" data-testid={`config-project-resolution-${project.id}`}>
          {project.resolution.strategy} {project.resolution.value}
        </span>
      ) : null}
      <span className="ec-mark" data-testid={`config-project-repos-${project.id}`}>
        {project.repos.length} {project.repos.length === 1 ? "repository" : "repositories"}
      </span>
      {targets.length > 0 ? (
        <span className="ec-mark" data-testid={`config-project-templates-${project.id}`}>
          built after {targets.join(", ")}
        </span>
      ) : null}
      {/* Two different problems with two different fixes: a reference names a catalog entry
          that does not exist; an unfinished binding is one nobody finished typing. Counted
          as one number, neither could be acted on. */}
      {unfinished > 0 ? (
        <span className="ec-mark warn" data-testid={`config-project-unfinished-${project.id}`}>
          {unfinished} template{unfinished === 1 ? "" : "s"} unfinished
        </span>
      ) : null}
      {unresolved > 0 ? (
        <span className="ec-mark bad" data-testid={`config-project-unresolved-${project.id}`}>
          {unresolved} unresolved
        </span>
      ) : null}
    </div>
  );
}

/** Every reference the card can judge without a call. An unfinished binding is NOT one. */
function unresolvedRefs(project: StudioProject, catalog: ConfigCatalog): number {
  let count = 0;
  if (!resolves(catalog, "agents", project.agent)) count++;
  if (!resolves(catalog, "trackers", project.tracker)) count++;
  for (const ref of project.repos) if (!resolveRepoRef(catalog, ref).ok) count++;
  // 2026-09-16-bedc: a template naming a project that does not exist is a reference like any
  // other. It was counted in neither bucket.
  for (const t of project.templates ?? [])
    if (t.project && !resolves(catalog, "projects", t.project)) count++;
  return count;
}
