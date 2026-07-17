"use client";

import { useState } from "react";
import type { ConfigEntityKind, StudioEntity } from "@/lib/configApi";
import { Button } from "@/components/ui/Button";
import { EntityTabs } from "./EntityTabs";
import { EntityCard } from "./EntityCard";
import { EntityDrawer } from "./EntityDrawer";
import { ChangesView } from "./ChangesView";
import { blankEntity, ENTITY_LABEL, ENTITY_SINGULAR } from "./entities";
import { useConfigCatalog } from "./useConfigCatalog";

// p0345: the Configuration studio shell — a route-driven catalog of the six
// editable entity kinds plus the Changes audit view. It loads the whole catalog
// once (the FK pickers need every list), renders the active kind's cards with a
// New button, and drives the create/edit drawer. The section is supplied by the
// route so navigation is URL-stable.

export type StudioSection = ConfigEntityKind | "changes";

interface DrawerState {
  kind: ConfigEntityKind;
  initial: StudioEntity;
  isNew: boolean;
}

export function ConfigStudio({ section }: { section: StudioSection }) {
  const { catalog, loading, error, reload } = useConfigCatalog();
  const [drawer, setDrawer] = useState<DrawerState | null>(null);

  const openNew = (kind: ConfigEntityKind) =>
    setDrawer({ kind, initial: blankEntity(kind), isNew: true });
  const openEdit = (kind: ConfigEntityKind, entity: StudioEntity) =>
    setDrawer({ kind, initial: entity, isNew: false });

  const onSaved = () => {
    setDrawer(null);
    void reload();
  };

  return (
    <main className="content-shell space-y-6">
      <header className="space-y-1">
        <h1 className="dsh-h1 font-semibold tracking-tight text-stone-900">Configuration</h1>
        <p className="dsh-body text-stone-400">
          the editable catalog &mdash; agents, trackers, repos, projects, MCP servers, secrets
        </p>
      </header>

      <EntityTabs section={section} />

      {error && (
        <p data-testid="config-load-error" className="dsh-body text-rose-600">
          Failed to load configuration: {error}
        </p>
      )}

      {section === "changes" ? (
        <ChangesView onReverted={reload} />
      ) : (
        <EntityCatalog
          kind={section}
          loading={loading}
          catalog={catalog}
          onNew={() => openNew(section)}
          onEdit={(entity) => openEdit(section, entity)}
        />
      )}

      {drawer && (
        <EntityDrawer
          kind={drawer.kind}
          initial={drawer.initial}
          isNew={drawer.isNew}
          catalog={catalog}
          onClose={() => setDrawer(null)}
          onSaved={onSaved}
        />
      )}
    </main>
  );
}

function EntityCatalog({
  kind,
  loading,
  catalog,
  onNew,
  onEdit,
}: {
  kind: ConfigEntityKind;
  loading: boolean;
  catalog: ReturnType<typeof useConfigCatalog>["catalog"];
  onNew: () => void;
  onEdit: (entity: StudioEntity) => void;
}) {
  const items = catalog[kind];
  return (
    <section className="space-y-4" data-testid={`config-catalog-${kind}`}>
      <div className="flex items-center">
        <h2 className="dsh-h3 font-semibold text-stone-900">{ENTITY_LABEL[kind]}</h2>
        <Button variant="primary" className="ml-auto" onClick={onNew} data-testid={`config-new-${kind}`}>
          New {ENTITY_SINGULAR[kind]}
        </Button>
      </div>

      {loading ? (
        <p className="dsh-body text-stone-400">Loading…</p>
      ) : items.length === 0 ? (
        <p data-testid={`config-empty-${kind}`} className="dsh-body text-stone-400">
          No {ENTITY_LABEL[kind].toLowerCase()} yet — create the first one.
        </p>
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {items.map((entity) => (
            <EntityCard
              key={entity.id}
              kind={kind}
              entity={entity}
              catalog={catalog}
              onEdit={() => onEdit(entity)}
            />
          ))}
        </div>
      )}
    </section>
  );
}
