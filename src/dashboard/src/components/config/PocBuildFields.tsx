"use client";

import { useState } from "react";
import type { PocBuildSetting, PocTargetSetting } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";
import { installPocTarget, type PocTargetInstalled } from "@/lib/pocsApi";
import { DrawerSection, NumberField, TextField, keep } from "./formFields";

// 2026-11-23-933dd: how a PoC target's image is built — by a pipeline of its source repo's host, which Install writes
// into that repo once. The names and job timeouts are written into the pipeline, so Install runs again after a change.
// Install starts a run on the saved configuration; it needs pocs.admin and the PoCs switch on.

/** The server's build defaults (PocBuildConfig); a target read without a build block is given these. */
export const POC_BUILD_DEFAULTS: PocBuildSetting = {
  pipeline: "poc-build", githubEnvironment: "poc-push", gitlabEnvironment: "poc-push", pushUserSecret: "POC_REGISTRY_USER",
  pushPasswordSecret: "POC_REGISTRY_PASSWORD", azureServiceConnection: "poc-registry", queueMinutes: 3, buildTimeoutMinutes: 12,
  scanTimeoutMinutes: 5, pushTimeoutMinutes: 3, pollSeconds: 15, reportKb: 1024,
};

const NAMES: { key: keyof PocBuildSetting; label: TextKey }[] = [
  { key: "pipeline", label: "config.pocs.build.pipeline" },
  { key: "githubEnvironment", label: "config.pocs.build.githubEnvironment" },
  { key: "gitlabEnvironment", label: "config.pocs.build.gitlabEnvironment" },
  { key: "pushUserSecret", label: "config.pocs.build.pushUserSecret" },
  { key: "pushPasswordSecret", label: "config.pocs.build.pushPasswordSecret" },
  { key: "azureServiceConnection", label: "config.pocs.build.azureServiceConnection" },
];

const NUMBERS: { key: keyof PocBuildSetting; label: TextKey }[] = [
  { key: "queueMinutes", label: "config.pocs.build.queueMinutes" },
  { key: "buildTimeoutMinutes", label: "config.pocs.build.buildTimeoutMinutes" },
  { key: "scanTimeoutMinutes", label: "config.pocs.build.scanTimeoutMinutes" },
  { key: "pushTimeoutMinutes", label: "config.pocs.build.pushTimeoutMinutes" },
  { key: "pollSeconds", label: "config.pocs.build.pollSeconds" },
  { key: "reportKb", label: "config.pocs.build.reportKb" },
];

export function PocBuildFields({ target, enabled, testId, onChange }: {
  target: PocTargetSetting; enabled: boolean; testId: string; onChange: (build: PocBuildSetting) => void;
}) {
  const t = useT();
  const build = target.build ?? POC_BUILD_DEFAULTS;
  const set = (patch: Partial<PocBuildSetting>) => onChange({ ...build, ...patch });
  return (
    <DrawerSection title={t("config.pocs.build.title")} testId={testId}>
      <span className="help">{t("config.pocs.build.help")}</span>
      <PocInstall target={target.name} enabled={enabled} testId={`${testId}-install`} />
      {NAMES.map(({ key, label }) => (
        <TextField key={key} label={t(label)} value={String(build[key])} mono testId={`${testId}-${key}`}
          onChange={(v) => set({ [key]: v })} />
      ))}
      {NUMBERS.map(({ key, label }) => (
        <NumberField key={key} label={t(label)} value={build[key] as number} testId={`${testId}-${key}`}
          onChange={(v) => set({ [key]: keep(v, build[key] as number) })} />
      ))}
      <span className="help">{t("config.pocs.build.wallTime")}</span>
    </DrawerSection>
  );
}

type Install = { status: "idle" } | { status: "running" } | { status: "done"; answer: PocTargetInstalled } | { status: "failed"; message: string };

function PocInstall({ target, enabled, testId }: { target: string; enabled: boolean; testId: string }) {
  const t = useT();
  const [install, setInstall] = useState<Install>({ status: "idle" });
  const run = () => {
    setInstall({ status: "running" });
    installPocTarget(target)
      .then((answer) => setInstall({ status: "done", answer }))
      .catch((err: unknown) => setInstall({ status: "failed", message: err instanceof Error ? err.message : String(err) }));
  };
  return (
    <div className="field">
      <button type="button" className="btn" data-testid={testId} disabled={!enabled || !target || install.status === "running"} onClick={run}>
        {install.status === "running" ? t("config.pocs.build.installing") : t("config.pocs.build.install")}
      </button>
      <span className="help" data-testid={`${testId}-said`}>
        {!enabled ? t("config.pocs.build.installOff")
          : install.status === "done" ? install.answer.message
          : install.status === "failed" ? t("config.pocs.build.installFailed", { message: install.message })
          : t("config.pocs.build.installHelp")}
      </span>
    </div>
  );
}
