"use client";

import { useEffect, useState } from "react";
import type { SettingKey, SettingValue } from "@/lib/configApi";
import { SettingsForm } from "./SettingsForm";
import { useSetting } from "./useSetting";
import { useCapabilities } from "./useCapabilities";
import { SETTING_ICON, SETTING_LABEL, SETTING_SUBTITLE } from "./settings";

// p0353: the Settings pane — one typed form per global settings singleton, sitting
// inside the config studio shell next to the entity catalog. It loads the doc, edits
// a local draft, and saves through the same live-applying path as entity CRUD. Save
// is enabled only when the draft differs from what is persisted, so an untouched form
// can't fire a no-op write.

export function SettingsStudio({ settingKey }: { settingKey: SettingKey }) {
  const { value, loading, error, saving, saveError, save } = useSetting(settingKey);
  const { capabilities } = useCapabilities();
  const [draft, setDraft] = useState<SettingValue | null>(null);

  // Reset the draft whenever a fresh value loads (initial load, or a reload after
  // save) — the form always starts from the persisted truth.
  useEffect(() => {
    setDraft(value);
  }, [value]);

  const dirty = draft !== null && value !== null && JSON.stringify(draft) !== JSON.stringify(value);

  async function onSave() {
    if (draft === null) return;
    try {
      await save(draft);
    } catch {
      /* saveError is surfaced below */
    }
  }

  return (
    <div className="mock-shell mock-config" data-testid={`settings-studio-${settingKey}`}>
      <main className="main">
        <div className="m-head">
          <div className="mt">
            <h1>
              <span aria-hidden="true" style={{ marginRight: 8 }}>
                {SETTING_ICON[settingKey]}
              </span>
              {SETTING_LABEL[settingKey]}
            </h1>
            <div className="msub">{SETTING_SUBTITLE[settingKey]}</div>
          </div>
          <button
            type="button"
            className="btn primary"
            onClick={onSave}
            disabled={!dirty || saving}
            data-testid="settings-save"
          >
            {saving ? "Saving…" : "Save changes"}
          </button>
        </div>

        {error && (
          <p data-testid="settings-load-error" className="msub" style={{ color: "var(--bad)" }}>
            Failed to load these settings: {error}
          </p>
        )}
        {saveError && (
          <p data-testid="settings-save-error" className="msub" style={{ color: "var(--bad)" }}>
            Save failed: {saveError}
          </p>
        )}

        <div className="db" data-testid="settings-form">
          {loading || draft === null ? (
            <div className="empty">Loading…</div>
          ) : (
            <SettingsForm
              settingKey={settingKey}
              value={draft}
              onChange={setDraft}
              capabilities={capabilities}
            />
          )}
        </div>
      </main>
    </div>
  );
}
