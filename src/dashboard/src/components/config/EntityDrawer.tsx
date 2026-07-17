"use client";

import { useState } from "react";
import type { ConfigEntityKind, StudioEntity, StudioProject } from "@/lib/configApi";
import { Button } from "@/components/ui/Button";
import { ENTITY_CLIENT, ENTITY_SINGULAR } from "./entities";
import { EntityForm } from "./EntityForm";
import { projectIntegrity } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the slide-over create/edit drawer. Owns the draft, persists via the
// entity's CRUD client, and gates Save on validity: a project cannot be saved
// until its wiring integrity is green (all refs resolve), which — together with
// the pick-only ref fields — makes a broken project unsavable from the UI.

export function EntityDrawer({
  kind,
  initial,
  isNew,
  catalog,
  onClose,
  onSaved,
}: {
  kind: ConfigEntityKind;
  initial: StudioEntity;
  isNew: boolean;
  catalog: ConfigCatalog;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [draft, setDraft] = useState<StudioEntity>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const idOk = draft.id.trim().length > 0;
  const projectOk =
    kind !== "projects" || projectIntegrity(catalog, draft as StudioProject).ok;
  const canSave = idOk && projectOk && !busy;

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const client = ENTITY_CLIENT[kind];
      if (isNew) await client.create(draft);
      else await client.update(draft.id, draft);
      onSaved();
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  }

  async function remove() {
    setBusy(true);
    setError(null);
    try {
      await ENTITY_CLIENT[kind].remove(draft.id);
      onSaved();
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  }

  return (
    <div className="fixed inset-0 z-40 flex justify-end" data-testid="config-drawer">
      <button
        type="button"
        aria-label="Close drawer"
        data-testid="config-drawer-scrim"
        className="absolute inset-0 bg-stone-900/20"
        onClick={onClose}
      />
      <aside className="relative z-10 flex h-full w-full max-w-md flex-col gap-4 overflow-y-auto border-l border-stone-200 bg-[var(--color-canvas)] p-6 shadow-xl">
        <header className="flex items-center gap-2">
          <h2 className="dsh-h3 font-semibold text-stone-900">
            {isNew ? `New ${ENTITY_SINGULAR[kind]}` : `Edit ${ENTITY_SINGULAR[kind]}`}
          </h2>
          <Button variant="subtle" className="ml-auto" onClick={onClose} data-testid="config-drawer-close">
            Close
          </Button>
        </header>

        <EntityForm kind={kind} draft={draft} onChange={setDraft} catalog={catalog} isNew={isNew} />

        {error && (
          <p data-testid="config-drawer-error" className="dsh-body text-rose-600">
            {error}
          </p>
        )}

        <footer className="mt-auto flex items-center gap-2 pt-2">
          <Button
            variant="primary"
            onClick={save}
            disabled={!canSave}
            data-testid="config-drawer-save"
          >
            {isNew ? "Create" : "Save"}
          </Button>
          {!isNew && (
            <Button
              variant="ghost"
              onClick={remove}
              disabled={busy}
              data-testid="config-drawer-delete"
              className="text-rose-600"
            >
              Delete
            </Button>
          )}
          {kind === "projects" && !projectOk && (
            <span data-testid="config-drawer-blocked" className="dsh-label text-amber-600">
              resolve all references to save
            </span>
          )}
        </footer>
      </aside>
    </div>
  );
}
