"use client";

import { useState } from "react";
import type {
  ConfigCapabilities,
  ConfigEntityKind,
  ConfigFinding,
  InheritedSandboxProjection,
  StudioConnection,
  StudioDesignSource,
  StudioEntity,
  StudioProject,
  StudioTracker,
} from "@/lib/configApi";
import { ENTITY_CLIENT, ENTITY_ICON, ENTITY_SINGULAR_KEY } from "./entities";
import { EntityForm } from "./EntityForm";
import { requiredFieldsFilled } from "./capabilityFields";
import { blockingFindings, useDraftFindings } from "./useDraftFindings";
import { trackerTabFor, trackerTabTitle } from "./trackerTabs";
import { projectIntegrity } from "./integrity";
import { unfinishedTemplates } from "./integrity";
import { cn } from "@/lib/utils";
import { setSecretValue as storeSecretValue } from "@/lib/secretValuesApi";
import type { ConfigCatalog } from "./useConfigCatalog";
import { useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

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
  capabilities,
  inheritedSandbox = null,
  onClose,
  onSaved,
}: {
  kind: ConfigEntityKind;
  initial: StudioEntity;
  isNew: boolean;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
  /** 2026-09-22-6968: what a project would inherit where it overrides nothing. */
  inheritedSandbox?: InheritedSandboxProjection | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const t = useT();
  const [draft, setDraft] = useState<StudioEntity>(initial);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [newSecretValue, setNewSecretValue] = useState("");

  // p0392: the server's own rules over the unsaved draft. p0391a made the server report
  // what is missing once it is running; a configuration that would disable a unit is
  // worth catching in the editor that produced it.
  const findings = useDraftFindings(kind, draft);
  const blocking = blockingFindings(findings);

  const idOk = draft.id.trim().length > 0;
  const projectOk =
    kind !== "projects" || projectIntegrity(catalog, draft as StudioProject).ok;
  // 2026-09-16-74a2: a binding missing its context, project, repo or template context is
  // refused where templates are fetched, and the row already says so. Letting the save
  // through stores a declaration that can never resolve, and the operator learns it on the
  // next run instead of on the form.
  const unfinished = kind === "projects" ? unfinishedTemplates(draft as StudioProject) : [];
  // p0345c: typed entities need a TYPE, and the capabilities descriptor's
  // required per-type fields must be filled before saving.
  const typedOk = typedEntityOk(kind, draft, capabilities);
  const canSave =
    idOk && projectOk && typedOk && unfinished.length === 0 && blocking.length === 0 && !busy;

  async function save() {
    setBusy(true);
    setError(null);
    try {
      const client = ENTITY_CLIENT[kind];
      // 2026-11-23-78d7: a new secret with a value is one secret run, which adds the catalog secret itself; a refused
      // value creates nothing. The field is cleared once the value is sent, whatever the answer.
      if (kind === "secrets" && isNew && newSecretValue !== "") {
        const value = newSecretValue;
        setNewSecretValue("");
        await storeSecretValue(draft.id, value);
      } else if (isNew) await client.create(draft);
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
        aria-label={t("config.drawer.closeDrawer")}
        data-testid="config-drawer-scrim"
        className="dbg open"
        onClick={onClose}
      />
      {/* 2026-09-16-74a2: one drawer element serves all seven kinds, so the project's
          extra width is a MODIFIER on it, not a second drawer. jsdom loads no stylesheet,
          so the 560 itself ships unasserted — what a test can pin is that the modifier is
          applied for a project and not for a secret. */}
      <aside
        className={cn("drawer open", kind === "projects" && "wide-project")}
        aria-label={t("config.drawer.label")}
      >
        <div className="dh">
          <div className="dh-ic">{ENTITY_ICON[kind]}</div>
          <h2>{t(isNew ? "config.drawer.new" : "config.drawer.edit", { kind: t(ENTITY_SINGULAR_KEY[kind]) })}</h2>
          <button
            type="button"
            className="x"
            aria-label={t("config.drawer.close")}
            onClick={onClose}
            data-testid="config-drawer-close"
          >
            ✕
          </button>
        </div>

        <div className="db">
          <EntityForm
            kind={kind}
            draft={draft}
            onChange={setDraft}
            catalog={catalog}
            capabilities={capabilities}
            inheritedSandbox={inheritedSandbox}
            isNew={isNew}
            findings={findings}
            newSecretValue={newSecretValue}
            onNewSecretValue={setNewSecretValue}
          />
          {error && (
            <p data-testid="config-drawer-error" style={{ color: "var(--bad)", fontSize: "12.5px" }}>
              {error}
            </p>
          )}
        </div>

        <div className="df">
          <span
            className="vmsg"
            data-testid={
              blocking.length > 0 || (kind === "projects" && (!projectOk || unfinished.length > 0))
                ? "config-drawer-blocked"
                : undefined
            }
          >
            {canSave
              ? isNew
                ? t("config.drawer.readyCreate")
                : t("config.drawer.readySave")
              : blocking.length > 0
              ? blockedMessage(t, kind, blocking[0], capabilities, draft)
              : kind === "projects" && !projectOk
              ? t("config.drawer.resolveRefs")
              : unfinished.length > 0
              ? t("config.drawer.templateUnfinished", { n: unfinished[0] + 1 })
              : !typedOk
              ? t("config.drawer.pickType")
              : t("config.drawer.fillRequired")}
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
              {t("common.delete")}
            </button>
          )}
          <button type="button" className="btn" onClick={onClose} data-testid="config-drawer-cancel">
            {t("common.cancel")}
          </button>
          <button
            type="button"
            className="btn primary"
            onClick={save}
            disabled={!canSave}
            data-testid="config-drawer-save"
          >
            {isNew ? t("config.drawer.create") : t("config.saveChanges")}
          </button>
        </div>
      </aside>
    </div>
  );
}

// Tracker/connection saves require a type; when the capabilities descriptor
// for that type is known, its required fields must be filled too. With
// capabilities unavailable only the type gate applies (never a false block).
function typedEntityOk(
  kind: ConfigEntityKind,
  draft: StudioEntity,
  capabilities: ConfigCapabilities | null,
): boolean {
  if (kind === "trackers") {
    const t = draft as StudioTracker;
    if (!t.type) return false;
    const d = capabilities?.trackerTypes.find((x) => x.type === t.type);
    return !d || requiredFieldsFilled(d.fields, t as unknown as Record<string, unknown>);
  }
  if (kind === "connections") {
    const c = draft as StudioConnection;
    if (!c.type) return false;
    const d = capabilities?.connectionTypes.find((x) => x.type === c.type);
    return !d || requiredFieldsFilled(d.fields, c as unknown as Record<string, unknown>);
  }
  if (kind === "design-sources") {
    // 2026-10-01-7f7aa: a source without a vendor or a secret cannot read anything.
    const d = draft as StudioDesignSource;
    return !!d.vendor && !!d.authSecret;
  }
  return true;
}

// 2026-10-06-cea8: the footer prints what the server said. It used to print
// "{field} is required here" for every blocking finding — wrong for one that is not about
// requiredness, and it read "type is required" on a tracker whose type was set. On the
// tabbed tracker form it also names the tab the field is on.
function blockedMessage(
  t: T,
  kind: ConfigEntityKind,
  finding: ConfigFinding,
  capabilities: ConfigCapabilities | null,
  draft: StudioEntity,
): string {
  if (kind !== "trackers" || !finding.field) return finding.reason;
  const tracker = draft as StudioTracker;
  const fields = capabilities?.trackerTypes.find((d) => d.type === tracker.type)?.fields ?? [];
  return t("config.drawer.blockedOnTab", { reason: finding.reason, tab: trackerTabTitle(t, trackerTabFor(finding.field, fields)) });
}
