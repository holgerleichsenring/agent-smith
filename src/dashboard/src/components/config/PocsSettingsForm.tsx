"use client";

import { useState } from "react";
import type { PocArchiveSetting, PocsSetting, PocTargetSetting, SettingValue, StudioEntity } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { CheckField, DrawerSection, NumberField, RefSelect, TextField, keep } from "./formFields";

// 2026-11-23-933da: the PoCs settings (agentsmith.yml pocs:) — the switch first, then the agent and tracker every
// person's poc- project names, then the targets. Every field stays editable while the switch is off, so a target can
// be set up before anyone sees the page. The server refuses what PocsConfig.Problems() names; this form only edits.

export interface PocsCatalog {
  agents: StudioEntity[];
  trackers: StudioEntity[];
  repos: StudioEntity[];
  connections: StudioEntity[];
}

export const DOMAIN_PLACEHOLDER = "{app}.poc.example.internal";

/** 2026-11-23-933db: the server's archive defaults (PocArchiveLimits). A value read without archive is given these, and
 *  every change sends archive back, because a save without it would reset the limits. */
export const POC_ARCHIVE_DEFAULTS: PocArchiveSetting = {
  maxZipMb: 25, maxUnpackedMb: 100, maxEntries: 2000, maxRatio: 100, maxPathChars: 240, maxFileMb: 10, maxBinaryMb: 20,
  maxDockerfileKb: 64, mismatchRatioPercent: 50, mismatchFileLimit: 100,
};

/** A new target carries the server's defaults; the address has none, only a placeholder. */
export function newPocTarget(): PocTargetSetting {
  return {
    name: "", sourceRepo: "", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
    domainPattern: null, lifetimeDays: 90, reminderDays: [14, 7, 1], perPersonLimit: 3,
  };
}

export function PocsSettingsForm({ value, onChange, catalog }: {
  value: PocsSetting; onChange: (v: SettingValue) => void; catalog: PocsCatalog;
}) {
  const t = useT();
  const archive = value.archive ?? POC_ARCHIVE_DEFAULTS;
  const set = (patch: Partial<PocsSetting>) => onChange({ ...value, archive, ...patch });
  const targets = value.targets ?? [];
  const setAt = (i: number, patch: Partial<PocTargetSetting>) =>
    set({ targets: targets.map((target, j) => (j === i ? { ...target, ...patch } : target)) });
  return (
    <>
      <CheckField label={t("config.pocs.enabled.label")} value={value.enabled} onChange={(v) => set({ enabled: v })}
        testId="setting-pocs-enabled" help={t("config.pocs.enabled.help")} />
      <span className="help">{t("config.pocs.projects.help")}</span>
      <RefSelect label={t("config.pocs.agent.label")} value={value.projectAgent ?? ""} options={catalog.agents}
        placeholder={t("config.pocs.pick")} testId="setting-pocs-agent" onChange={(v) => set({ projectAgent: v || null })} />
      <RefSelect label={t("config.pocs.tracker.label")} value={value.projectTracker ?? ""} options={catalog.trackers}
        placeholder={t("config.pocs.pick")} testId="setting-pocs-tracker" onChange={(v) => set({ projectTracker: v || null })} />
      {targets.length === 0 && <span className="help" data-testid="setting-pocs-no-target">{t("config.pocs.targets.none")}</span>}
      {targets.map((target, i) => (
        <DrawerSection key={i} title={target.name || t("config.pocs.target.untitled", { number: i + 1 })} defaultOpen
          testId={`setting-pocs-target-${i}`}>
          <PocTargetFields target={target} index={i} catalog={catalog} onChange={(patch) => setAt(i, patch)} />
          <button type="button" className="btn" data-testid={`setting-pocs-target-${i}-remove`}
            onClick={() => set({ targets: targets.filter((_, j) => j !== i) })}>
            {t("config.pocs.target.remove")}
          </button>
        </DrawerSection>
      ))}
      <button type="button" className="btn" data-testid="setting-pocs-target-add"
        onClick={() => set({ targets: [...targets, newPocTarget()] })}>
        {t("config.pocs.target.add")}
      </button>
      <DrawerSection title={t("config.pocs.archive.title")} testId="setting-pocs-archive">
        <span className="help">{t("config.pocs.archive.help")}</span>
        <PocArchiveFields archive={archive} onChange={(patch) => set({ archive: { ...archive, ...patch } })} />
      </DrawerSection>
    </>
  );
}

// Each field: its wire name, its label and the one-line reason the bound exists.
const ARCHIVE_FIELDS: { key: keyof PocArchiveSetting; label: TextKey; help: TextKey }[] = [
  { key: "maxZipMb", label: "config.pocs.archive.maxZipMb", help: "config.pocs.archive.maxZipMb.help" },
  { key: "maxUnpackedMb", label: "config.pocs.archive.maxUnpackedMb", help: "config.pocs.archive.maxUnpackedMb.help" },
  { key: "maxEntries", label: "config.pocs.archive.maxEntries", help: "config.pocs.archive.maxEntries.help" },
  { key: "maxRatio", label: "config.pocs.archive.maxRatio", help: "config.pocs.archive.maxRatio.help" },
  { key: "maxPathChars", label: "config.pocs.archive.maxPathChars", help: "config.pocs.archive.maxPathChars.help" },
  { key: "maxFileMb", label: "config.pocs.archive.maxFileMb", help: "config.pocs.archive.maxFileMb.help" },
  { key: "maxBinaryMb", label: "config.pocs.archive.maxBinaryMb", help: "config.pocs.archive.maxBinaryMb.help" },
  { key: "maxDockerfileKb", label: "config.pocs.archive.maxDockerfileKb", help: "config.pocs.archive.maxDockerfileKb.help" },
  { key: "mismatchRatioPercent", label: "config.pocs.archive.mismatchRatioPercent", help: "config.pocs.archive.mismatchRatioPercent.help" },
  { key: "mismatchFileLimit", label: "config.pocs.archive.mismatchFileLimit", help: "config.pocs.archive.mismatchFileLimit.help" },
];

function PocArchiveFields({ archive, onChange }: { archive: PocArchiveSetting; onChange: (patch: Partial<PocArchiveSetting>) => void }) {
  const t = useT();
  return (
    <>
      {ARCHIVE_FIELDS.map(({ key, label, help }) => (
        <NumberField key={key} label={t(label)} value={archive[key]} help={t(help)} testId={`setting-pocs-archive-${key}`}
          onChange={(v) => onChange({ [key]: keep(v, archive[key]) })} />
      ))}
    </>
  );
}

function PocTargetFields({ target, index, catalog, onChange }: {
  target: PocTargetSetting; index: number; catalog: PocsCatalog; onChange: (patch: Partial<PocTargetSetting>) => void;
}) {
  const t = useT();
  const id = `setting-pocs-target-${index}`;
  return (
    <>
      <TextField label={t("config.pocs.target.name")} value={target.name} mono testId={`${id}-name`}
        help={t("config.pocs.target.name.help")} onChange={(v) => onChange({ name: v })} />
      <SourceRepoField value={target.sourceRepo} catalog={catalog} testId={`${id}-source`} onChange={(v) => onChange({ sourceRepo: v })} />
      <TextField label={t("config.pocs.target.root")} value={target.root} mono testId={`${id}-root`}
        help={t("config.pocs.target.root.help")} onChange={(v) => onChange({ root: v })} />
      <TextField label={t("config.pocs.target.deployRoot")} value={target.deployRoot} mono testId={`${id}-deploy-root`}
        help={t("config.pocs.target.deployRoot.help")} onChange={(v) => onChange({ deployRoot: v })} />
      <TextField label={t("config.pocs.target.templatePath")} value={target.templatePath} mono testId={`${id}-template`}
        onChange={(v) => onChange({ templatePath: v })} />
      <TextField label={t("config.pocs.target.domainPattern")} value={target.domainPattern ?? ""} mono testId={`${id}-domain`}
        placeholder={DOMAIN_PLACEHOLDER} help={t("config.pocs.target.domainPattern.help")}
        onChange={(v) => onChange({ domainPattern: v || null })} />
      <NumberField label={t("config.pocs.target.lifetime")} value={target.lifetimeDays} testId={`${id}-lifetime`}
        onChange={(v) => onChange({ lifetimeDays: keep(v, target.lifetimeDays) })} />
      <ReminderDaysField value={target.reminderDays} testId={`${id}-reminders`} onChange={(v) => onChange({ reminderDays: v })} />
      <NumberField label={t("config.pocs.target.limit")} value={target.perPersonLimit} testId={`${id}-limit`}
        help={t("config.pocs.target.limit.help")} onChange={(v) => onChange({ perPersonLimit: keep(v, target.perPersonLimit) })} />
    </>
  );
}

// The source repository is named the way a project names a repo: a catalog name or connection/name. The suggestions are
// the catalog's repos and its connections; a wildcard is refused by the server, since a glob would bind many repos.
function SourceRepoField({ value, catalog, testId, onChange }: {
  value: string; catalog: PocsCatalog; testId: string; onChange: (v: string) => void;
}) {
  const t = useT();
  const listId = `${testId}-options`;
  const options = [...catalog.repos.map((r) => r.id), ...catalog.connections.map((c) => `${c.id}/`)];
  return (
    <div className="field">
      <label>
        {t("config.pocs.target.sourceRepo")}
        <span className="help">{t("config.pocs.target.sourceRepo.help")}</span>
      </label>
      <input type="text" className="mono" list={listId} data-testid={testId} value={value} onChange={(e) => onChange(e.target.value)} />
      <datalist id={listId}>{options.map((option) => <option key={option} value={option} />)}</datalist>
    </div>
  );
}

// Typed as text so "14, " can be typed through; a part that is not a whole number is left out of the value.
function ReminderDaysField({ value, testId, onChange }: { value: number[]; testId: string; onChange: (v: number[]) => void }) {
  const t = useT();
  const [raw, setRaw] = useState(value.join(", "));
  return (
    <TextField label={t("config.pocs.target.reminders")} value={raw} mono testId={testId}
      help={t("config.pocs.target.reminders.help")}
      onChange={(text) => {
        setRaw(text);
        onChange(text.split(",").map((part) => part.trim()).filter((part) => /^\d+$/.test(part)).map(Number));
      }} />
  );
}
