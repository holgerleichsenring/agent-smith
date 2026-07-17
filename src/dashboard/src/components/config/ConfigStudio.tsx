"use client";

import { useState } from "react";
import type { ConfigEntityKind, StudioEntity } from "@/lib/configApi";
import { fetchConfigExportYml } from "@/lib/configApi";
import { Button } from "@/components/ui/Button";
import { EntityCard } from "./EntityCard";
import { EntityDrawer } from "./EntityDrawer";
import { ChangesView } from "./ChangesView";
import { blankEntity, ENTITY_LABEL, ENTITY_SINGULAR, ENTITY_SUBTITLE } from "./entities";
import { useConfigCatalog } from "./useConfigCatalog";

// p0345: the Configuration studio — the catalog of the editable entity kinds
// plus the Changes audit view. It loads the whole catalog once (the FK pickers
// need every list), renders the active kind's cards with a New button, and
// drives the create/edit drawer. The section is supplied by the route so
// navigation is URL-stable.
// p0343b (mock fidelity): the entity TABS are gone — the AppRail's CATALOG
// section is the section switcher now. The content area is the mock's layout:
// entity title row (title + subtitle + green New button), the thesis note with
// the agentsmith.yml export, then the entity cards.

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
    <main className="content-shell space-y-5">
      <header className="flex items-start gap-4">
        <div className="space-y-1">
          <h1 className="dsh-h1 font-semibold tracking-tight text-stone-900">
            {section === "changes" ? "Changes" : ENTITY_LABEL[section]}
          </h1>
          <p className="dsh-body text-stone-400">
            {section === "changes"
              ? "the attributed, revertible audit trail of every catalog edit"
              : ENTITY_SUBTITLE[section]}
          </p>
        </div>
        {section !== "changes" && (
          <Button
            variant="primary"
            className="ml-auto"
            onClick={() => openNew(section)}
            data-testid={`config-new-${section}`}
          >
            New {ENTITY_SINGULAR[section]}
          </Button>
        )}
      </header>

      <ThesisNote />

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

// p0343b: the mock's thesis note — the catalog IS the source of truth, and the
// export renders it as a loader-round-trippable agentsmith.yml on demand.
function ThesisNote() {
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const onExport = async () => {
    setExporting(true);
    setExportError(null);
    try {
      const yml = await fetchConfigExportYml();
      const url = URL.createObjectURL(new Blob([yml], { type: "text/yaml" }));
      const a = document.createElement("a");
      a.href = url;
      a.download = "agentsmith.yml";
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setExportError((err as Error).message);
    } finally {
      setExporting(false);
    }
  };

  return (
    <div
      data-testid="config-thesis-note"
      className="flex flex-wrap items-center gap-3 rounded-md border border-dashed border-stone-300 bg-[var(--color-canvas-soft)] px-4 py-3"
    >
      <p className="min-w-0 flex-1 dsh-body text-stone-500">
        The source of truth is this catalog — refs are picked, never typed.
        Export renders it as <code className="font-mono dsh-mono text-stone-600">agentsmith.yml</code>.
        No hand-edited config map.
      </p>
      <Button
        variant="ghost"
        onClick={() => void onExport()}
        disabled={exporting}
        data-testid="config-export-yml"
      >
        {exporting ? "Exporting…" : "Export agentsmith.yml"}
      </Button>
      {exportError && (
        <span data-testid="config-export-error" className="dsh-label text-rose-600">
          export failed: {exportError}
        </span>
      )}
    </div>
  );
}

function EntityCatalog({
  kind,
  loading,
  catalog,
  onEdit,
}: {
  kind: ConfigEntityKind;
  loading: boolean;
  catalog: ReturnType<typeof useConfigCatalog>["catalog"];
  onEdit: (entity: StudioEntity) => void;
}) {
  const items = catalog[kind];
  return (
    <section className="space-y-4" data-testid={`config-catalog-${kind}`}>
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
