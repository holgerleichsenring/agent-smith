"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { SettingKey, SettingValue } from "@/lib/configApi";
import { SettingsForm } from "./SettingsForm";
import { useSetting } from "./useSetting";
import { useSettingDefault } from "./useSettingDefault";
import { useCapabilities } from "./useCapabilities";
import {
  SETTING_ICON,
  SETTING_LABEL_KEY,
  SETTING_SUBTITLE,
  changedCount,
  settingGroup,
  type SettingGroupId,
} from "./settings";
import { settingRefusal } from "./settingsRefusal";
import { refusalIn } from "@/lib/apiResponse";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { useT } from "@/hooks/useT";

// 2026-11-28-9bd1: a settings GROUP page — the small limit and cadence pages, each a collapsible
// section with its own title, one-line description and unchanged form. A section reads its saved
// value and its default; one whose value differs opens and says how many values differ. The one
// Save writes every section with an unsaved edit through the same PUT the single page uses, and is
// gated the same way: nothing to save, or any section refusing its draft, disables it.

interface SectionState {
  dirty: boolean;
  refused: boolean;
  /** Values that differ from the default; null while either is unknown. */
  changed: number | null;
  /** Its value and default have answered (or failed), so it has opened or stayed closed for good. */
  settled: boolean;
}

type Saver = () => Promise<void>;

export function SettingsGroupStudio({ groupId, open = null }: { groupId: SettingGroupId; open?: SettingKey | null }) {
  const t = useT();
  const group = settingGroup(groupId);
  const [onlyChanged, setOnlyChanged] = useState(false);
  const [states, setStates] = useState<Partial<Record<SettingKey, SectionState>>>({});
  const [saving, setSaving] = useState(false);
  const savers = useRef<Partial<Record<SettingKey, Saver>>>({});

  const report = useCallback((key: SettingKey, state: SectionState) => {
    setStates((prev) => {
      const old = prev[key];
      if (old && old.dirty === state.dirty && old.refused === state.refused && old.changed === state.changed && old.settled === state.settled) return prev;
      return { ...prev, [key]: state };
    });
  }, []);

  const list = group.members.map((key) => ({ key, state: states[key] }));
  const anyDirty = list.some((s) => s.state?.dirty);
  // Only an edit the Save would send can block it.
  const anyRefused = list.some((s) => s.state?.dirty && s.state.refused);
  // A section with an unsaved edit always shows, so nothing is saved while out of sight.
  const shows = (state: SectionState | undefined) =>
    !onlyChanged || state === undefined || state.dirty || state.changed === null || state.changed > 0;
  const noneShown = onlyChanged && list.every((s) => !shows(s.state));
  // A deep link scrolls once every section has opened or stayed closed, so none pushes it away after.
  const allSettled = list.every((s) => s.state?.settled);

  async function onSave() {
    if (!anyDirty || anyRefused) return;
    setSaving(true);
    try {
      // In order, each on its own: a refused save keeps its draft and error, the others still land.
      for (const { key, state } of list) {
        if (state?.dirty) await savers.current[key]?.();
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mock-shell mock-config" data-testid={`settings-group-${groupId}`}>
      <main className="main">
        <div className="m-head">
          <div className="mt">
            <h1>
              <span aria-hidden="true" style={{ marginRight: 8 }}>
                {group.icon}
              </span>
              {t(group.label)}
            </h1>
            <div className="msub">{t(group.subtitle)}</div>
          </div>
          <button
            type="button"
            className="btn primary"
            onClick={onSave}
            disabled={!anyDirty || saving || anyRefused}
            data-testid="settings-save"
          >
            {saving ? t("config.saving") : t("config.saveChanges")}
          </button>
        </div>

        <div className="db sgroup" data-testid="settings-group-sections">
          <label className="sgroup-filter">
            <input
              type="checkbox"
              checked={onlyChanged}
              onChange={(event) => setOnlyChanged(event.target.checked)}
              data-testid="settings-only-changed"
            />
            {t("config.settingGroup.onlyChanged")}
          </label>
          {noneShown && (
            <p className="msub" data-testid="settings-none-changed">
              {t("config.settingGroup.noneChanged")}
            </p>
          )}
          {list.map(({ key, state }) => (
            <SettingsSection
              key={key}
              settingKey={key}
              forceOpen={open === key}
              scrollReady={allSettled}
              hidden={!shows(state)}
              report={report}
              savers={savers.current}
            />
          ))}
        </div>
      </main>
    </div>
  );
}

function SettingsSection({
  settingKey,
  forceOpen,
  scrollReady,
  hidden,
  report,
  savers,
}: {
  settingKey: SettingKey;
  forceOpen: boolean;
  scrollReady: boolean;
  hidden: boolean;
  report: (key: SettingKey, state: SectionState) => void;
  savers: Partial<Record<SettingKey, Saver>>;
}) {
  const t = useT();
  const { value, loading, error, saveError, save } = useSetting(settingKey);
  const defaults = useSettingDefault(settingKey);
  const { capabilities } = useCapabilities();
  const [draft, setDraft] = useState<SettingValue | null>(null);
  const [expanded, setExpanded] = useState(forceOpen);
  const decided = useRef(false);
  const scrolled = useRef(false);
  const root = useRef<HTMLElement>(null);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  const dirty = draft !== null && value !== null && JSON.stringify(draft) !== JSON.stringify(value);
  const refusal = draft === null ? null : settingRefusal(t, settingKey, draft);
  const loadRefusal = refusalIn(error);
  const changed = value !== null && defaults.value !== null ? changedCount(value, defaults.value) : null;

  // The section opens itself once, when its value and default are first known; after that it is
  // the reader's to open and close, so a save back to the default never snaps it shut.
  useEffect(() => {
    if (decided.current || loading || !defaults.settled) return;
    decided.current = true;
    if (changed !== null && changed > 0) setExpanded(true);
  }, [loading, defaults.settled, changed]);

  // A deep link to a former page opens its section and brings it into view once the page has settled.
  useEffect(() => {
    if (forceOpen) setExpanded(true);
  }, [forceOpen]);
  // A refusal, a failed load or a refused save is never left inside a closed section.
  useEffect(() => {
    if (error || saveError) setExpanded(true);
  }, [error, saveError]);
  useEffect(() => {
    if (!forceOpen || !scrollReady || scrolled.current) return;
    scrolled.current = true;
    root.current?.scrollIntoView?.({ block: "start" });
  }, [forceOpen, scrollReady]);

  useEffect(() => {
    report(settingKey, { dirty, refused: refusal !== null, changed, settled: !loading && defaults.settled });
  }, [report, settingKey, dirty, refusal, changed, loading, defaults.settled]);

  savers[settingKey] = async () => {
    if (draft === null || refusal !== null) return;
    try {
      await save(draft);
    } catch {
      /* saveError is surfaced in the section */
    }
  };

  const bodyId = `settings-section-${settingKey}-body`;
  return (
    <section
      ref={root}
      id={`settings-section-${settingKey}`}
      className="sgroup-sec"
      hidden={hidden}
      data-testid={`settings-section-${settingKey}`}
      data-open={expanded ? "true" : "false"}
    >
      <h2 className="sgroup-h">
        <button
          type="button"
          className="sgroup-toggle"
          aria-expanded={expanded}
          aria-controls={bodyId}
          onClick={() => setExpanded((o) => !o)}
          data-testid={`settings-section-${settingKey}-toggle`}
        >
          <span className="chev" aria-hidden="true">
            {expanded ? "▾" : "▸"}
          </span>
          <span className="sgroup-title">
            <span className="sgroup-name">
              <span aria-hidden="true" className="sgroup-icon">
                {SETTING_ICON[settingKey]}
              </span>
              {t(SETTING_LABEL_KEY[settingKey])}
            </span>
            <span className="sgroup-sub">{t(SETTING_SUBTITLE[settingKey])}</span>
          </span>
          {changed !== null && changed > 0 && (
            <span className="sgroup-changed" data-testid={`settings-section-${settingKey}-changed`}>
              {t("config.settingGroup.changed", { count: changed })}
            </span>
          )}
        </button>
      </h2>
      {expanded && (
        <div className="sgroup-b" id={bodyId}>
          {loadRefusal ? (
            <RefusalSurface refusal={loadRefusal} surface={t("config.settings.surface")} />
          ) : (
            error && (
              <p data-testid={`settings-load-error-${settingKey}`} className="msub" style={{ color: "var(--bad)" }}>
                {t("config.settings.loadFailed", { message: error.message })}
              </p>
            )
          )}
          {refusal && (
            <p data-testid={`settings-refusal-${settingKey}`} className="msub" style={{ color: "var(--bad)" }}>
              {refusal}
            </p>
          )}
          {saveError && (
            <p data-testid={`settings-save-error-${settingKey}`} className="msub" style={{ color: "var(--bad)" }}>
              {t("config.saveFailed", { message: saveError.message })}
            </p>
          )}
          {!error &&
            (loading || draft === null ? (
              <div className="empty">{t("config.loading")}</div>
            ) : (
              <SettingsForm
                settingKey={settingKey}
                value={draft}
                onChange={setDraft}
                capabilities={capabilities}
                persisted={value}
                saveError={saveError?.message ?? null}
              />
            ))}
        </div>
      )}
    </section>
  );
}
