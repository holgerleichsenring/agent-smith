"use client";

import type { ProjectMachine, ProjectMachineHoster, StudioEntity, StudioProject } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { RefSelect, TextField } from "./formFields";

// 2026-11-25-9992h: the machine identity agent-smith acts under — its name and commit email, and per hoster the secret its
// pushes, pull requests, comments and merges authenticate with. A GitHub App adds its app id and installation id, and its
// bot address as the GitHub commit email. The block is written only once a control here is touched; clearing every field
// clears it. A hoster left empty acts with each repository's own token, and the project's findings say so.
// 2026-11-25-d0e2: an Azure DevOps entry with a tenant id and a client id is a service principal; its secret is the client secret.

const HOSTERS = [
  { key: "github", label: "GitHub" },
  { key: "gitlab", label: "GitLab" },
  { key: "azureDevops", label: "Azure DevOps" },
] as const;

type HosterKey = (typeof HOSTERS)[number]["key"];

const EMPTY: ProjectMachine = { name: "", email: "" };

export function ProjectMachineSection({ project, onChange, secrets }: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  secrets: StudioEntity[];
}) {
  const t = useT();
  const machine = project.machine ?? EMPTY;
  const set = (patch: Partial<ProjectMachine>) => onChange({ ...project, machine: { ...machine, ...patch } });
  const setHoster = (key: HosterKey, patch: Partial<ProjectMachineHoster>) => {
    const next = { auth: "", ...(machine[key] ?? {}), ...patch };
    const empty = !next.auth && !next.appId && !next.installationId && !next.email && !next.tenantId && !next.clientId;
    set({ [key]: empty ? null : next });
  };
  return (
    <>
      <p className="help">{t("config.machine.help")}</p>
      <TextField label={t("config.machine.name.label")} value={machine.name} placeholder="agent-smith-bot"
        help={t("config.machine.name.help")} testId="form-field-machine-name" onChange={(v) => set({ name: v })} />
      <TextField label={t("config.machine.email.label")} value={machine.email} placeholder="agent-smith-bot@example.com" mono
        help={t("config.machine.email.help")} testId="form-field-machine-email" onChange={(v) => set({ email: v })} />
      {HOSTERS.map(({ key, label }) => (
        <fieldset key={key} className="field" data-testid={`form-machine-${key}`}>
          <legend>{label}</legend>
          <RefSelect label={t("config.machine.auth.label")} value={machine[key]?.auth ?? ""} options={secrets}
            placeholder={t("config.machine.auth.placeholder")} testId={`form-field-machine-${key}-auth`}
            onChange={(v) => setHoster(key, { auth: v })} />
          {key === "github" && (
            <>
              <TextField label={t("config.machine.appId.label")} value={machine.github?.appId ?? ""} mono
                help={t("config.machine.appId.help")} testId="form-field-machine-github-appId"
                onChange={(v) => setHoster("github", { appId: v || null })} />
              <TextField label={t("config.machine.installationId.label")} value={machine.github?.installationId ?? ""} mono
                testId="form-field-machine-github-installationId" onChange={(v) => setHoster("github", { installationId: v || null })} />
            </>
          )}
          {key === "azureDevops" && (
            <>
              <TextField label={t("config.machine.tenantId.label")} value={machine.azureDevops?.tenantId ?? ""} mono
                help={t("config.machine.tenantId.help")} testId="form-field-machine-azureDevops-tenantId"
                onChange={(v) => setHoster("azureDevops", { tenantId: v || null })} />
              <TextField label={t("config.machine.clientId.label")} value={machine.azureDevops?.clientId ?? ""} mono
                testId="form-field-machine-azureDevops-clientId" onChange={(v) => setHoster("azureDevops", { clientId: v || null })} />
            </>
          )}
          <TextField label={t("config.machine.hosterEmail.label")} value={machine[key]?.email ?? ""} mono
            help={t("config.machine.hosterEmail.help")} testId={`form-field-machine-${key}-email`}
            onChange={(v) => setHoster(key, { email: v || null })} />
        </fieldset>
      ))}
    </>
  );
}
