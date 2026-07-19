"use client";

import { useRef, useState } from "react";
import type { ConfigEntityKind, StudioEntity } from "@/lib/configApi";
import { ConfigStoreNotEmptyError, fetchConfigExportYml, importConfigYml } from "@/lib/configApi";
import { EntityCard } from "./EntityCard";
import { EntityDrawer } from "./EntityDrawer";
import { ChangesView } from "./ChangesView";
import { RepoInventory } from "./RepoInventory";
import { blankEntity, ENTITY_ICON, ENTITY_LABEL, ENTITY_SINGULAR, ENTITY_SUBTITLE } from "./entities";
import { useCapabilities } from "./useCapabilities";
import { useConfigCatalog } from "./useConfigCatalog";

// p0345: the Configuration studio — the catalog of the editable entity kinds
// plus the Changes audit view. It loads the whole catalog once (the FK pickers
// need every list), renders the active kind's cards with a New button, and
// drives the create/edit drawer. The section is supplied by the route.
// p0343c (pixel identity): emits the config-studio.html DOM verbatim — the
// .m-head title row with the green "New X" .btn, the dashed .yaml-note thesis
// with the working agentsmith.yml export, the .list of .ecard cards, and the
// mock .empty state.

export type StudioSection = ConfigEntityKind | "changes";

interface DrawerState {
  kind: ConfigEntityKind;
  initial: StudioEntity;
  isNew: boolean;
}

export function ConfigStudio({ section }: { section: StudioSection }) {
  const { catalog, loading, error, reload } = useConfigCatalog();
  // p0345c: the capabilities descriptor is loaded ONCE (module-cached) and
  // feeds every type/provider/strategy dropdown in the drawer forms.
  const { capabilities } = useCapabilities();
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
    <div className="mock-shell mock-config" data-testid="config-studio">
      <main className="main">
        <div className="m-head">
          <div className="mt">
            <h1>{section === "changes" ? "Changes" : ENTITY_LABEL[section]}</h1>
            <div className="msub">
              {section === "changes"
                ? "Every config edit — who, when, what — revertible. The audit trail a hand-edited config map never had."
                : ENTITY_SUBTITLE[section]}
            </div>
          </div>
          {section !== "changes" && (
            <button
              type="button"
              className="btn primary"
              onClick={() => openNew(section)}
              data-testid={`config-new-${section}`}
            >
              <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                <path d="M8 3v10M3 8h10" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
              </svg>
              New {ENTITY_SINGULAR[section]}
            </button>
          )}
        </div>

        <ThesisNote reload={reload} />

        {error && (
          <p data-testid="config-load-error" className="msub" style={{ color: "var(--bad)" }}>
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
            capabilities={capabilities}
            onClose={() => setDrawer(null)}
            onSaved={onSaved}
          />
        )}
      </main>
    </div>
  );
}

// p0343b: the mock's thesis note — the catalog IS the source of truth, and the
// export renders it as a loader-round-trippable agentsmith.yml on demand.
// p0352: the import is the inverse — upload an agentsmith.yml straight into the
// DB store (guarded: a non-empty store confirms an overwrite first).
function ThesisNote({ reload }: { reload: () => void | Promise<void> }) {
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [importing, setImporting] = useState(false);
  const [importMsg, setImportMsg] = useState<string | null>(null);
  const [importError, setImportError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

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

  const runImport = async (yaml: string, force: boolean) => {
    const count = await importConfigYml(yaml, force);
    setImportMsg(`Imported ${count} config entities.`);
    await reload();
  };

  const onFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ""; // let the same file be picked again after an error
    if (!file) return;
    setImporting(true);
    setImportError(null);
    setImportMsg(null);
    try {
      const yaml = await file.text();
      try {
        await runImport(yaml, false);
      } catch (err) {
        if (err instanceof ConfigStoreNotEmptyError) {
          if (window.confirm(`${err.message}\n\nOverwrite the current config? History is kept.`)) {
            await runImport(yaml, true);
          }
        } else {
          throw err;
        }
      }
    } catch (err) {
      setImportError((err as Error).message);
    } finally {
      setImporting(false);
    }
  };

  return (
    <div className="yaml-note" data-testid="config-thesis-note">
      <span>◇</span>
      <div>
        The source of truth is this catalog — refs are picked, never typed, so a project can’t
        point at an agent that doesn’t exist. <b>No hand-edited config map.</b>
      </div>
      {exportError && (
        <span data-testid="config-export-error" style={{ color: "var(--bad)" }}>
          export failed: {exportError}
        </span>
      )}
      {importError && (
        <span data-testid="config-import-error" style={{ color: "var(--bad)" }}>
          import failed: {importError}
        </span>
      )}
      {importMsg && (
        <span data-testid="config-import-ok" style={{ color: "var(--good, #2e7d32)" }}>
          {importMsg}
        </span>
      )}
      <input
        ref={fileRef}
        type="file"
        accept=".yml,.yaml,text/yaml"
        style={{ display: "none" }}
        onChange={(e) => void onFile(e)}
        data-testid="config-import-file"
      />
      <button
        type="button"
        className="btn pull"
        onClick={() => fileRef.current?.click()}
        disabled={importing}
        data-testid="config-import-yml"
      >
        {importing ? "Importing…" : "Import agentsmith.yml ↥"}
      </button>
      <button
        type="button"
        className="btn"
        onClick={() => void onExport()}
        disabled={exporting}
        data-testid="config-export-yml"
      >
        {exporting ? "Exporting…" : "Export agentsmith.yml ↧"}
      </button>
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
  const cards = loading ? (
    <div className="empty">Loading…</div>
  ) : items.length === 0 ? (
    <div className="empty" data-testid={`config-empty-${kind}`}>
      <div className="ei">{ENTITY_ICON[kind]}</div>
      No {ENTITY_LABEL[kind].toLowerCase()} yet. Create your first one.
    </div>
  ) : (
    items.map((entity) => (
      <EntityCard
        key={entity.id}
        kind={kind}
        entity={entity}
        catalog={catalog}
        onEdit={() => onEdit(entity)}
      />
    ))
  );

  // p0345c: the Repositories page shows BOTH worlds — the per-connection
  // DISCOVERED inventory (read-only, referenced-by badges) above the legacy
  // standalone catalog under its own section rule.
  if (kind === "repos") {
    return (
      <div data-testid="config-catalog-repos">
        <section>
          <div className="section-head">
            <h2>Discovered per connection</h2>
            <span className="sh-sub">what the discovery cache actually found — wire these as conn/Name</span>
          </div>
          {loading ? (
            <div className="empty">Loading…</div>
          ) : (
            <RepoInventory connections={catalog.connections} projects={catalog.projects} />
          )}
        </section>
        <section>
          <div className="section-head">
            <h2>Standalone catalog</h2>
            <span className="sh-sub">individually registered repos — the legacy world</span>
          </div>
          <div className="list">{cards}</div>
        </section>
      </div>
    );
  }

  return (
    <div className="list" data-testid={`config-catalog-${kind}`}>
      {cards}
    </div>
  );
}
