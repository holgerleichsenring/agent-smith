"use client";

import { useState } from "react";
import type {
  PocArchiveSetting, PocGitOpsSetting, PocRolloutSetting, PocScanSetting, PocsSetting, PocTargetSetting, PocVariablesSetting, SettingValue, StudioEntity,
} from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { CheckField, DrawerSection, NumberField, RefSelect, TextField, keep } from "./formFields";
import { newPocRegistry, PocRegistryFields } from "./PocRegistryFields";
import { POC_BUILD_DEFAULTS, PocBuildFields } from "./PocBuildFields";
import { newPocSecrets, PocSecretsFields } from "./PocSecretsFields";
import { newPocDeployBlocks, PocDeployFields, withPocDeployBlocks } from "./PocDeployFields";
import { newPocArgo, PocArgoFields } from "./PocArgoFields";

// 2026-11-23-933da: the PoCs settings (agentsmith.yml pocs:) — the switch first, then the agent and tracker every
// person's poc- project names, then the targets. Every field stays editable while the switch is off, so a target can
// be set up before anyone sees the page. The server refuses what PocsConfig.Problems() names; this form only edits.

export interface PocsCatalog {
  agents: StudioEntity[];
  trackers: StudioEntity[];
  repos: StudioEntity[];
  connections: StudioEntity[];
  /** 2026-11-23-933de: the registry pull token is picked from the secrets catalog. */
  secrets: StudioEntity[];
}

export const DOMAIN_PLACEHOLDER = "{app}.poc.example.internal";

/** 2026-11-23-933db: the server's archive defaults (PocArchiveLimits). A value read without archive is given these, and
 *  every change sends archive back, because a save without it would reset the limits. */
export const POC_ARCHIVE_DEFAULTS: PocArchiveSetting = {
  maxZipMb: 25, maxUnpackedMb: 100, maxEntries: 2000, maxRatio: 100, maxPathChars: 240, maxFileMb: 10, maxBinaryMb: 20,
  maxDockerfileKb: 64, mismatchRatioPercent: 50, mismatchFileLimit: 100,
};

/** 2026-11-23-933dc: the server's scan defaults (PocScanConfig). Like archive, scan and blockedRetentionDays are always
 *  sent back. 2026-11-23-933dg: an empty image means the server's pinned default; an operator sets one only for a mirror. */
export const POC_SCAN_DEFAULTS: PocScanSetting = {
  osvImage: "", timeoutSeconds: 300, unfixedCriticalBlock: true, secretMinConfidence: 8, trivyImage: "",
};

export const POC_BLOCKED_RETENTION_DAYS_DEFAULT = 7;

/** 2026-11-23-933dg: the server's default images, shown as placeholders. */
export const OSV_IMAGE_PLACEHOLDER =
  "ghcr.io/google/osv-scanner:v2.2.4@sha256:f7ba4be68bac8086b1f88fd598fdca1ca67239c79ad2c2b5c78e03a82e5187c4";

export const TRIVY_IMAGE_PLACEHOLDER =
  "aquasec/trivy:0.76.2@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb";

/** 2026-11-23-933dg: the server's variables bounds (PocVariablesConfig); like archive, always sent back. */
export const POC_VARIABLES_DEFAULTS: PocVariablesSetting = { maxNames: 64, maxValueKb: 4 };

/** 2026-11-23-c81eb: the server's GitOps commit defaults (PocGitOpsConfig); like archive, always sent back. */
export const POC_GITOPS_DEFAULTS: PocGitOpsSetting = { commitAttempts: 3, maxFolderFiles: 500 };

/** 2026-11-23-c81ec: the server's rollout defaults (PocRolloutConfig) and the PoC run wall-time budget; like archive,
 *  always sent back. */
export const POC_ROLLOUT_DEFAULTS: PocRolloutSetting = {
  pollSeconds: 10, deadlineSeconds: 600, endMarginSeconds: 60, commitAllowanceSeconds: 120,
};

export const POC_RUN_WALL_TIME_SECONDS_DEFAULT = 2700;

/** A new target carries the server's defaults; the address has none, only a placeholder. */
export function newPocTarget(): PocTargetSetting {
  return {
    name: "", sourceRepo: "", root: "pocs", deployRoot: "deploy", templatePath: "poc-template",
    domainPattern: null, lifetimeDays: 90, reminderDays: [14, 7, 1], perPersonLimit: 3, registry: newPocRegistry(),
    build: { ...POC_BUILD_DEFAULTS }, secrets: newPocSecrets(), ...newPocDeployBlocks(), argo: newPocArgo(),
  };
}

export function PocsSettingsForm({ value, onChange, catalog }: {
  value: PocsSetting; onChange: (v: SettingValue) => void; catalog: PocsCatalog;
}) {
  const t = useT();
  const archive = value.archive ?? POC_ARCHIVE_DEFAULTS;
  const scan = value.scan ?? POC_SCAN_DEFAULTS;
  const blockedRetentionDays = value.blockedRetentionDays ?? POC_BLOCKED_RETENTION_DAYS_DEFAULT;
  const variables = value.variables ?? POC_VARIABLES_DEFAULTS;
  const gitops = value.gitops ?? POC_GITOPS_DEFAULTS;
  const rollout = value.rollout ?? POC_ROLLOUT_DEFAULTS;
  const runWallTimeSeconds = value.runWallTimeSeconds ?? POC_RUN_WALL_TIME_SECONDS_DEFAULT;
  // 2026-11-23-933dg: a target read without a secrets block is given the defaults and sends them back;
  // 2026-11-23-c81eb: so is one without an ingress, auth, network, quota or container block; 2026-11-23-c81ec: or argo.
  const targets = (value.targets ?? []).map((target) => withPocDeployBlocks({
    ...target, secrets: target.secrets ?? newPocSecrets(), argo: target.argo ?? newPocArgo(),
  }));
  const set = (patch: Partial<PocsSetting>) =>
    onChange({ ...value, archive, scan, blockedRetentionDays, variables, gitops, rollout, runWallTimeSeconds, targets, ...patch });
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
          <PocTargetFields target={target} index={i} catalog={catalog} enabled={value.enabled} onChange={(patch) => setAt(i, patch)} />
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
      <DrawerSection title={t("config.pocs.scan.title")} testId="setting-pocs-scan">
        <span className="help" data-testid="setting-pocs-scan-secret-fixed">{t("config.pocs.scan.secretFixed")}</span>
        <PocScanFields scan={scan} blockedRetentionDays={blockedRetentionDays}
          onScan={(patch) => set({ scan: { ...scan, ...patch } })}
          onRetention={(days) => set({ blockedRetentionDays: days })} />
      </DrawerSection>
      <DrawerSection title={t("config.pocs.variables.title")} testId="setting-pocs-variables">
        <span className="help">{t("config.pocs.variables.help")}</span>
        <NumberField label={t("config.pocs.variables.maxNames")} value={variables.maxNames} testId="setting-pocs-variables-maxNames"
          help={t("config.pocs.variables.maxNames.help")}
          onChange={(v) => set({ variables: { ...variables, maxNames: keep(v, variables.maxNames) } })} />
        <NumberField label={t("config.pocs.variables.maxValueKb")} value={variables.maxValueKb} testId="setting-pocs-variables-maxValueKb"
          help={t("config.pocs.variables.maxValueKb.help")}
          onChange={(v) => set({ variables: { ...variables, maxValueKb: keep(v, variables.maxValueKb) } })} />
      </DrawerSection>
      <DrawerSection title={t("config.pocs.gitops.title")} testId="setting-pocs-gitops">
        <span className="help">{t("config.pocs.gitops.help")}</span>
        <NumberField label={t("config.pocs.gitops.commitAttempts")} value={gitops.commitAttempts} testId="setting-pocs-gitops-commitAttempts"
          help={t("config.pocs.gitops.commitAttempts.help")}
          onChange={(v) => set({ gitops: { ...gitops, commitAttempts: keep(v, gitops.commitAttempts) } })} />
        <NumberField label={t("config.pocs.gitops.maxFolderFiles")} value={gitops.maxFolderFiles} testId="setting-pocs-gitops-maxFolderFiles"
          help={t("config.pocs.gitops.maxFolderFiles.help")}
          onChange={(v) => set({ gitops: { ...gitops, maxFolderFiles: keep(v, gitops.maxFolderFiles) } })} />
      </DrawerSection>
      <DrawerSection title={t("config.pocs.rollout.title")} testId="setting-pocs-rollout">
        <span className="help">{t("config.pocs.rollout.help")}</span>
        {ROLLOUT_FIELDS.map(({ key, label, help }) => (
          <NumberField key={key} label={t(label)} value={rollout[key]} help={t(help)} testId={`setting-pocs-rollout-${key}`}
            onChange={(v) => set({ rollout: { ...rollout, [key]: keep(v, rollout[key]) } })} />
        ))}
        <NumberField label={t("config.pocs.runWallTimeSeconds")} value={runWallTimeSeconds} testId="setting-pocs-runWallTimeSeconds"
          help={t("config.pocs.runWallTimeSeconds.help")}
          onChange={(v) => set({ runWallTimeSeconds: keep(v, runWallTimeSeconds) })} />
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

const ROLLOUT_FIELDS: { key: keyof PocRolloutSetting; label: TextKey; help: TextKey }[] = [
  { key: "pollSeconds", label: "config.pocs.rollout.pollSeconds", help: "config.pocs.rollout.pollSeconds.help" },
  { key: "deadlineSeconds", label: "config.pocs.rollout.deadlineSeconds", help: "config.pocs.rollout.deadlineSeconds.help" },
  { key: "endMarginSeconds", label: "config.pocs.rollout.endMarginSeconds", help: "config.pocs.rollout.endMarginSeconds.help" },
  { key: "commitAllowanceSeconds", label: "config.pocs.rollout.commitAllowanceSeconds", help: "config.pocs.rollout.commitAllowanceSeconds.help" },
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

function PocScanFields({ scan, blockedRetentionDays, onScan, onRetention }: {
  scan: PocScanSetting; blockedRetentionDays: number;
  onScan: (patch: Partial<PocScanSetting>) => void; onRetention: (days: number) => void;
}) {
  const t = useT();
  return (
    <>
      <TextField label={t("config.pocs.scan.osvImage")} value={scan.osvImage} mono testId="setting-pocs-scan-osvImage"
        placeholder={OSV_IMAGE_PLACEHOLDER} help={t("config.pocs.scan.osvImage.help")}
        onChange={(v) => onScan({ osvImage: v })} />
      <TextField label={t("config.pocs.scan.trivyImage")} value={scan.trivyImage ?? ""} mono testId="setting-pocs-scan-trivyImage"
        placeholder={TRIVY_IMAGE_PLACEHOLDER} help={t("config.pocs.scan.trivyImage.help")}
        onChange={(v) => onScan({ trivyImage: v })} />
      <NumberField label={t("config.pocs.scan.timeoutSeconds")} value={scan.timeoutSeconds} testId="setting-pocs-scan-timeoutSeconds"
        help={t("config.pocs.scan.timeoutSeconds.help")} onChange={(v) => onScan({ timeoutSeconds: keep(v, scan.timeoutSeconds) })} />
      <CheckField label={t("config.pocs.scan.unfixedCriticalBlock")} value={scan.unfixedCriticalBlock}
        testId="setting-pocs-scan-unfixedCriticalBlock" help={t("config.pocs.scan.unfixedCriticalBlock.help")}
        onChange={(v) => onScan({ unfixedCriticalBlock: v })} />
      <NumberField label={t("config.pocs.scan.secretMinConfidence")} value={scan.secretMinConfidence}
        testId="setting-pocs-scan-secretMinConfidence" help={t("config.pocs.scan.secretMinConfidence.help")}
        onChange={(v) => onScan({ secretMinConfidence: keep(v, scan.secretMinConfidence) })} />
      <NumberField label={t("config.pocs.blockedRetentionDays")} value={blockedRetentionDays}
        testId="setting-pocs-blockedRetentionDays" help={t("config.pocs.blockedRetentionDays.help")}
        onChange={(v) => onRetention(keep(v, blockedRetentionDays))} />
    </>
  );
}

function PocTargetFields({ target, index, catalog, enabled, onChange }: {
  target: PocTargetSetting; index: number; catalog: PocsCatalog; enabled: boolean; onChange: (patch: Partial<PocTargetSetting>) => void;
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
      <PocRegistryFields registry={target.registry} secrets={catalog.secrets} testId={`${id}-registry`}
        onChange={(registry) => onChange({ registry })} />
      <PocBuildFields target={target} enabled={enabled} testId={`${id}-build`} onChange={(build) => onChange({ build })} />
      <PocSecretsFields secrets={target.secrets} testId={`${id}-secrets`} onChange={(secrets) => onChange({ secrets })} />
      <PocDeployFields target={target} testId={id} onChange={onChange} />
      <PocArgoFields argo={target.argo} target={target.name} secrets={catalog.secrets} testId={id} onChange={(argo) => onChange({ argo })} />
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
