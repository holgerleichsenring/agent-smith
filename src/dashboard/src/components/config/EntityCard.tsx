"use client";

import type { ConfigEntityKind, StudioEntity } from "@/lib/configApi";
import type {
  StudioAgent,
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
import { resolves } from "./integrity";

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
          Edit
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
      return (
        <div className="flex flex-col gap-3">
          <div className="flex flex-wrap gap-x-8 gap-y-2">
            <FieldBlock label="provider">{a.provider || "—"}</FieldBlock>
            <FieldBlock label="coding model">{a.models.coding || "—"}</FieldBlock>
            <FieldBlock label="scan model">{a.models.scan || "—"}</FieldBlock>
          </div>
          <div className="flex flex-wrap gap-2">
            <WiringChip
              label="key"
              value={a.keySecret}
              resolved={resolves(catalog, "secrets", a.keySecret)}
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
            <span className="dsh-mono font-mono font-semibold text-stone-900">{p.id}</span>
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
            {p.repos.map((repoId) => (
              <WiringChip
                key={repoId}
                label="repo"
                value={repoId}
                resolved={resolves(catalog, "repos", repoId)}
              />
            ))}
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
