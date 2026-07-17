"use client";

import { useState } from "react";
import type { ConfigEntityKind, StudioEntity, StudioProject } from "@/lib/configApi";
import { ENTITY_CLIENT, ENTITY_ICON, ENTITY_SINGULAR } from "./entities";
import { EntityForm } from "./EntityForm";
import { projectIntegrity } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";

// p0345: the slide-over create/edit drawer. Owns the draft, persists via the
// entity's CRUD client, and gates Save on validity: a project cannot be saved
// until its wiring integrity is green (all refs resolve).
// p0343c (pixel identity): emits the config-studio.html drawer DOM verbatim —
// .dbg scrim, .drawer with .dh (icon block + title + .x close), the .db form
// body and the .df footer (validity message left, Cancel + green Save right).

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
    <div data-testid="config-drawer">
      <button
        type="button"
        aria-label="Close drawer"
        data-testid="config-drawer-scrim"
        className="dbg open"
        onClick={onClose}
      />
      <aside className="drawer open" aria-label="Create or edit">
        <div className="dh">
          <div className="dh-ic">{ENTITY_ICON[kind]}</div>
          <h2>{isNew ? `New ${ENTITY_SINGULAR[kind]}` : `Edit ${ENTITY_SINGULAR[kind]}`}</h2>
          <button
            type="button"
            className="x"
            aria-label="Close"
            onClick={onClose}
            data-testid="config-drawer-close"
          >
            ✕
          </button>
        </div>

        <div className="db">
          <EntityForm kind={kind} draft={draft} onChange={setDraft} catalog={catalog} isNew={isNew} />
          {error && (
            <p data-testid="config-drawer-error" style={{ color: "var(--bad)", fontSize: "12.5px" }}>
              {error}
            </p>
          )}
        </div>

        <div className="df">
          <span className="vmsg" data-testid={kind === "projects" && !projectOk ? "config-drawer-blocked" : undefined}>
            {canSave
              ? isNew
                ? "Ready to create"
                : "Ready to save"
              : kind === "projects" && !projectOk
              ? "resolve all references to save"
              : "Fill the required fields"}
          </span>
          {!isNew && (
            <button
              type="button"
              className="btn"
              onClick={remove}
              disabled={busy}
              data-testid="config-drawer-delete"
              style={{ color: "var(--bad)" }}
            >
              Delete
            </button>
          )}
          <button type="button" className="btn" onClick={onClose} data-testid="config-drawer-cancel">
            Cancel
          </button>
          <button
            type="button"
            className="btn primary"
            onClick={save}
            disabled={!canSave}
            data-testid="config-drawer-save"
          >
            {isNew ? "Create" : "Save changes"}
          </button>
        </div>
      </aside>
    </div>
  );
}
