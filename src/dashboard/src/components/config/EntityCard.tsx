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
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ENTITY_BADGE } from "./entities";
import { FieldBlock, WiringChip } from "./primitives";
import type { ConfigCatalog } from "./useConfigCatalog";
import { resolves, resolveRepoRef } from "./integrity";

// p0345: one entity card — mono id + type badge in the header, key attributes as
// joined field-blocks, and reference wiring rendered as chips that resolve
// against the live catalog (dangling refs go rose). Edit opens the drawer.

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
      className="card-content flex flex-col gap-3 p-4"
    >
      <div className="flex items-center gap-2">
        <span className="dsh-mono font-mono font-semibold text-stone-900">{entity.id}</span>
        <Badge tone="neutral" testId={`config-card-badge-${entity.id}`}>
          {ENTITY_BADGE[kind]}
        </Badge>
        <Button
          variant="subtle"
          className="ml-auto"
          data-testid={`config-card-edit-${entity.id}`}
          onClick={onEdit}
        >
          edit ›
        </Button>
      </div>
      <CardBody kind={kind} entity={entity} catalog={catalog} />
    </div>
  );
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
      // p0343b: list the model roles ACTUALLY present on the entry — an entry
      // with primary/scout/planning shows those three, never a hardcoded
      // coding/scan pair with phantom "—" dashes for roles it doesn't have.
      const roles = Object.entries(a.models).filter(([, model]) => model);
      return (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap gap-x-8 gap-y-2">
            <FieldBlock label="provider">{a.provider || "—"}</FieldBlock>
            {roles.map(([role, model]) => (
              <FieldBlock key={role} label={`${role} model`} testId={`config-card-model-${a.id}-${role}`}>
                {model}
              </FieldBlock>
            ))}
          </div>
          <div className="flex flex-wrap gap-2">
            {/* No key ref at all is an honest neutral "key —"; rose is reserved
                for a ref that NAMES a secret missing from the catalog. */}
            <WiringChip
              label="key"
              value={a.keySecret ?? ""}
              resolved={!a.keySecret || resolves(catalog, "secrets", a.keySecret)}
              testId={`config-card-key-${a.id}`}
            />
          </div>
        </div>
      );
    }
    case "trackers": {
      const t = entity as StudioTracker;
      return (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap gap-x-8 gap-y-2">
            <FieldBlock label="type">{t.type || "—"}</FieldBlock>
            <FieldBlock label="org">{t.org || "—"}</FieldBlock>
            <FieldBlock label="project">{t.project || "—"}</FieldBlock>
          </div>
          <div className="flex flex-wrap gap-2">
            <WiringChip
              label="auth"
              value={t.authSecret}
              resolved={resolves(catalog, "secrets", t.authSecret)}
            />
          </div>
        </div>
      );
    }
    case "connections": {
      const c = entity as StudioConnection;
      return (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap gap-x-8 gap-y-2">
            <FieldBlock label="type">{c.type || "—"}</FieldBlock>
            <FieldBlock label="organization">{c.organization || "—"}</FieldBlock>
            <FieldBlock label="project">{c.project || "—"}</FieldBlock>
            <FieldBlock label="default branch">{c.defaultBranch || "—"}</FieldBlock>
          </div>
          <div className="flex flex-wrap gap-2">
            <WiringChip
              label="auth"
              value={c.authSecret}
              resolved={resolves(catalog, "secrets", c.authSecret)}
              testId={`config-card-connection-auth-${c.id}`}
            />
          </div>
        </div>
      );
    }
    case "repos": {
      const r = entity as StudioRepo;
      return (
        <div className="flex flex-wrap gap-x-8 gap-y-2">
          <FieldBlock label="name">{r.name || "—"}</FieldBlock>
          <FieldBlock label="branch">{r.branch || "—"}</FieldBlock>
        </div>
      );
    }
    case "projects": {
      const p = entity as StudioProject;
      return (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap items-center gap-2" data-testid={`config-card-wiring-${p.id}`}>
            <WiringChip
              label="agent"
              value={p.agent}
              resolved={resolves(catalog, "agents", p.agent)}
              testId={`config-card-agent-${p.id}`}
            />
            <span className="text-stone-400">→</span>
            {/* p0343b mock fidelity: the project itself is the GREEN center of
                the wires row — Smith-green marks the entity being wired. */}
            <span
              data-testid={`config-card-project-chip-${p.id}`}
              className="badge-pill border border-emerald-300 bg-emerald-50 font-mono dsh-label font-semibold text-emerald-700"
            >
              {p.id}
            </span>
            <span className="text-stone-400">←</span>
            <WiringChip
              label="tracker"
              value={p.tracker}
              resolved={resolves(catalog, "trackers", p.tracker)}
              testId={`config-card-tracker-${p.id}`}
            />
          </div>
          <div className="flex flex-wrap gap-2">
            {p.repos.length === 0 && <span className="dsh-label text-stone-400">no repos</span>}
            {p.repos.map((repoId) => {
              // p0345b: conn-scoped discovery refs ("conn/Name") resolve
              // against the connections catalog; plain refs against repos.
              const result = resolveRepoRef(catalog, repoId);
              return (
                <WiringChip
                  key={repoId}
                  label={repoId.includes("/") ? "conn repo" : "repo"}
                  value={repoId}
                  resolved={result.ok}
                  testId={`config-card-repo-${p.id}-${repoId}`}
                />
              );
            })}
          </div>
          <div className="flex flex-wrap gap-x-8 gap-y-2">
            <FieldBlock label="trigger">{p.trigger || "—"}</FieldBlock>
            <FieldBlock label="pipelines">{p.pipelines.join(", ") || "—"}</FieldBlock>
          </div>
        </div>
      );
    }
    case "mcp-servers": {
      const m = entity as StudioMcpServer;
      return (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap gap-x-8 gap-y-2">
            <FieldBlock label="transport">{m.transport || "—"}</FieldBlock>
            <FieldBlock label="url">{m.url || "—"}</FieldBlock>
          </div>
          <div className="flex flex-wrap gap-2">
            <WiringChip
              label="auth"
              value={m.authSecret}
              resolved={resolves(catalog, "secrets", m.authSecret)}
            />
          </div>
        </div>
      );
    }
    case "secrets": {
      const s = entity as StudioSecret;
      return (
        <div
          data-testid={`config-secret-redaction-${s.id}`}
          className="flex items-center gap-2 rounded-md border border-amber-300 bg-amber-50 px-3 py-1.5"
        >
          <span className="dsh-label font-semibold uppercase tracking-wide text-amber-700">
            env-name only
          </span>
          <span className="dsh-mono font-mono text-amber-700">••••••••</span>
          <span className="dsh-label text-amber-600">value resolved from runtime</span>
        </div>
      );
    }
  }
}
