"use client";

import type { PocArgoSetting, StudioEntity } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { DraftCheckPanel } from "./DraftCheckPanel";
import { DrawerSection, NumberField, SelectField, TextField, keep } from "./formFields";

// 2026-11-23-c81ec: the Argo CD that deploys a target's PoC folders through one ApplicationSet, which Install upserts.
// The role token is a secret the dashboard never sees; Test connection reads its expiry on the server and shows it as a
// step, with the ApplicationSet as it is on Argo CD now. The server refuses what PocArgoConfig.Problems() names.

export const ARGO_URL_PLACEHOLDER = "https://argocd.example.internal";
export const ARGO_REPO_URL_PLACEHOLDER = "https://git.example.internal/platform/poc-source.git";

/** The server's defaults (PocArgoConfig). A target read without an argo block is given these and sends them back,
 *  because a save without the block would reset it. The URL, token, project and repository have none. */
export function newPocArgo(): PocArgoSetting {
  return {
    url: "", tokenSecret: "", project: "", namespace: "argocd", destinationServer: "https://kubernetes.default.svc",
    repoUrl: "", applicationSet: null, tokenWarnDays: 14, generatorRequeueSeconds: 30,
  };
}

export function PocArgoFields({ argo, target, secrets, testId, onChange }: {
  argo: PocArgoSetting | null | undefined; target: string; secrets: StudioEntity[]; testId: string;
  onChange: (argo: PocArgoSetting) => void;
}) {
  const t = useT();
  const value = argo ?? newPocArgo();
  const set = (patch: Partial<PocArgoSetting>) => onChange({ ...value, ...patch });
  return (
    <DrawerSection title={t("config.pocs.argo.title")} testId={`${testId}-argo`}>
      <span className="help">{t("config.pocs.argo.help")}</span>
      <TextField label={t("config.pocs.argo.url")} value={value.url} mono testId={`${testId}-argo-url`}
        placeholder={ARGO_URL_PLACEHOLDER} help={t("config.pocs.argo.url.help")} onChange={(v) => set({ url: v.trim() })} />
      <SelectField label={t("config.pocs.argo.tokenSecret")} value={value.tokenSecret} options={secrets.map((s) => s.id)}
        placeholder={t("config.pocs.pick")} testId={`${testId}-argo-token-secret`} help={t("config.pocs.argo.tokenSecret.help")}
        onChange={(v) => set({ tokenSecret: v })} />
      <TextField label={t("config.pocs.argo.project")} value={value.project} mono testId={`${testId}-argo-project`}
        help={t("config.pocs.argo.project.help")} onChange={(v) => set({ project: v.trim() })} />
      <TextField label={t("config.pocs.argo.namespace")} value={value.namespace} mono testId={`${testId}-argo-namespace`}
        help={t("config.pocs.argo.namespace.help")} onChange={(v) => set({ namespace: v.trim() })} />
      <TextField label={t("config.pocs.argo.destinationServer")} value={value.destinationServer} mono
        testId={`${testId}-argo-destination-server`} help={t("config.pocs.argo.destinationServer.help")}
        onChange={(v) => set({ destinationServer: v.trim() })} />
      <TextField label={t("config.pocs.argo.repoUrl")} value={value.repoUrl} mono testId={`${testId}-argo-repo-url`}
        placeholder={ARGO_REPO_URL_PLACEHOLDER} help={t("config.pocs.argo.repoUrl.help")}
        onChange={(v) => set({ repoUrl: v.trim() })} />
      <TextField label={t("config.pocs.argo.applicationSet")} value={value.applicationSet ?? ""} mono
        testId={`${testId}-argo-application-set`} placeholder={`pocs-${target || "<target>"}`}
        help={t("config.pocs.argo.applicationSet.help")} onChange={(v) => set({ applicationSet: v.trim() || null })} />
      <NumberField label={t("config.pocs.argo.tokenWarnDays")} value={value.tokenWarnDays} testId={`${testId}-argo-token-warn-days`}
        help={t("config.pocs.argo.tokenWarnDays.help")} onChange={(v) => set({ tokenWarnDays: keep(v, value.tokenWarnDays) })} />
      <NumberField label={t("config.pocs.argo.generatorRequeueSeconds")} value={value.generatorRequeueSeconds}
        testId={`${testId}-argo-generator-requeue-seconds`} help={t("config.pocs.argo.generatorRequeueSeconds.help")}
        onChange={(v) => set({ generatorRequeueSeconds: keep(v, value.generatorRequeueSeconds) })} />
      <DraftCheckPanel kind="poc-argo" target={target} draft={value} label={t("config.pocs.argo.test")}
        help={t("config.pocs.argo.test.help")} testId={`${testId}-argo-check`} />
    </DrawerSection>
  );
}
