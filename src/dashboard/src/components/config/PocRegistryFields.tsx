"use client";

import type { PocPullMode, PocRegistrySetting, StudioEntity } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { DraftCheckPanel } from "./DraftCheckPanel";
import { DrawerSection, SelectField, TextField } from "./formFields";

// 2026-11-23-933de: a PoC target's container registry — where its images are read from with one pull-only token. Test
// connection checks the values in the form (PoCs on or off); the server refuses what PocRegistryConfig.Problems() names.

export const LOGIN_SERVER_PLACEHOLDER = "pocregistry.azurecr.io";

/** A new registry block carries the server's defaults; the login server, user and token have none. */
export function newPocRegistry(): PocRegistrySetting {
  return { type: "acr", loginServer: "", repository: "pocs", pullUser: "", pullAuth: "", pullMode: "pull_secret" };
}

const PULL_MODES: PocPullMode[] = ["pull_secret", "cluster_pulls"];

export function PocRegistryFields({ registry, secrets, testId, onChange }: {
  registry: PocRegistrySetting | null; secrets: StudioEntity[]; testId: string;
  onChange: (registry: PocRegistrySetting) => void;
}) {
  const t = useT();
  const value = registry ?? newPocRegistry();
  const set = (patch: Partial<PocRegistrySetting>) => onChange({ ...value, ...patch });
  return (
    <DrawerSection title={t("config.pocs.registry.title")} defaultOpen testId={testId}>
      <span className="help">{t("config.pocs.registry.help")}</span>
      <SelectField label={t("config.pocs.registry.type")} value={value.type} options={[{ value: "acr", label: "Azure Container Registry" }]}
        testId={`${testId}-type`} onChange={() => set({ type: "acr" })} />
      <TextField label={t("config.pocs.registry.loginServer")} value={value.loginServer} mono testId={`${testId}-login-server`}
        placeholder={LOGIN_SERVER_PLACEHOLDER} help={t("config.pocs.registry.loginServer.help")}
        onChange={(v) => set({ loginServer: v.trim() })} />
      <TextField label={t("config.pocs.registry.repository")} value={value.repository} mono testId={`${testId}-repository`}
        help={t("config.pocs.registry.repository.help")} onChange={(v) => set({ repository: v.trim() })} />
      <TextField label={t("config.pocs.registry.pullUser")} value={value.pullUser} mono testId={`${testId}-pull-user`}
        help={t("config.pocs.registry.pullUser.help")} onChange={(v) => set({ pullUser: v.trim() })} />
      <SelectField label={t("config.pocs.registry.pullAuth")} value={value.pullAuth} options={secrets.map((s) => s.id)}
        placeholder={t("config.pocs.pick")} testId={`${testId}-pull-auth`} help={t("config.pocs.registry.pullAuth.help")}
        onChange={(v) => set({ pullAuth: v })} />
      <PullModeField value={value.pullMode} testId={`${testId}-pull-mode`} onChange={(pullMode) => set({ pullMode })} />
      <DraftCheckPanel kind="poc-registry" draft={value} label={t("config.pocs.registry.test")}
        help={t("config.pocs.registry.test.help")} testId={`${testId}-check`} />
    </DrawerSection>
  );
}

function PullModeField({ value, testId, onChange }: { value: PocPullMode; testId: string; onChange: (v: PocPullMode) => void }) {
  const t = useT();
  return (
    <div className="field" data-testid={testId}>
      <label>{t("config.pocs.registry.pullMode")}</label>
      {PULL_MODES.map((mode) => (
        <label key={mode} className="flex items-start gap-2">
          <input type="radio" name={testId} value={mode} checked={value === mode} data-testid={`${testId}-${mode}`}
            onChange={() => onChange(mode)} />
          <span>
            {t(`config.pocs.registry.pullMode.${mode}`)}
            <span className="help">{t(`config.pocs.registry.pullMode.${mode}.help`)}</span>
          </span>
        </label>
      ))}
    </div>
  );
}
