"use client";

import type { ProjectMachine, ProjectMachineHoster, StudioEntity } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { TextField, RefSelect } from "./formFields";

// 2026-11-25-9992h: the machine identity agent-smith acts under — its name and commit email, and per hoster the secret its
// pushes, pull requests, comments and merges authenticate with. A GitHub App adds its app id and installation id, and its
// bot address as the GitHub commit email. A hoster left empty acts with each repository's own token, and the findings say so.
// 2026-11-25-d0e2: an Azure DevOps entry with a tenant id and a client id is a service principal; its secret is the client secret.
// 2026-11-25-0873: one set of fields for a project's machine: block and for pocs.machine; testId prefixes keep them apart.

const HOSTERS = [
  { key: "github", label: "GitHub" },
  { key: "gitlab", label: "GitLab" },
  { key: "azureDevops", label: "Azure DevOps" },
] as const;

type HosterKey = (typeof HOSTERS)[number]["key"];

const EMPTY: ProjectMachine = { name: "", email: "" };

export function MachineIdentityFields({ value, onChange, secrets, testPrefix }: {
  value: ProjectMachine | null | undefined;
  onChange: (next: ProjectMachine) => void;
  secrets: StudioEntity[];
  testPrefix: string;
}) {
  const t = useT();
  const machine = value ?? EMPTY;
  const id = (field: string) => `${testPrefix}-${field}`;
  const set = (patch: Partial<ProjectMachine>) => onChange({ ...machine, ...patch });
  const setHoster = (key: HosterKey, patch: Partial<ProjectMachineHoster>) => {
    const next = { auth: "", ...(machine[key] ?? {}), ...patch };
    const empty = !next.auth && !next.appId && !next.installationId && !next.email && !next.tenantId && !next.clientId;
    set({ [key]: empty ? null : next });
  };
  return (
    <>
      <TextField label={t("config.machine.name.label")} value={machine.name} placeholder="agent-smith-bot"
        help={t("config.machine.name.help")} testId={id("name")} onChange={(v) => set({ name: v })} />
      <TextField label={t("config.machine.email.label")} value={machine.email} placeholder="agent-smith-bot@example.com" mono
        help={t("config.machine.email.help")} testId={id("email")} onChange={(v) => set({ email: v })} />
      {HOSTERS.map(({ key, label }) => (
        <fieldset key={key} className="field" data-testid={id(`${key}-group`)}>
          <legend>{label}</legend>
          <RefSelect label={t("config.machine.auth.label")} value={machine[key]?.auth ?? ""} options={secrets}
            placeholder={t("config.machine.auth.placeholder")} testId={id(`${key}-auth`)}
            onChange={(v) => setHoster(key, { auth: v })} />
          {key === "github" && (
            <>
              <TextField label={t("config.machine.appId.label")} value={machine.github?.appId ?? ""} mono
                help={t("config.machine.appId.help")} testId={id("github-appId")}
                onChange={(v) => setHoster("github", { appId: v || null })} />
              <TextField label={t("config.machine.installationId.label")} value={machine.github?.installationId ?? ""} mono
                testId={id("github-installationId")} onChange={(v) => setHoster("github", { installationId: v || null })} />
            </>
          )}
          {key === "azureDevops" && (
            <>
              <TextField label={t("config.machine.tenantId.label")} value={machine.azureDevops?.tenantId ?? ""} mono
                help={t("config.machine.tenantId.help")} testId={id("azureDevops-tenantId")}
                onChange={(v) => setHoster("azureDevops", { tenantId: v || null })} />
              <TextField label={t("config.machine.clientId.label")} value={machine.azureDevops?.clientId ?? ""} mono
                testId={id("azureDevops-clientId")} onChange={(v) => setHoster("azureDevops", { clientId: v || null })} />
            </>
          )}
          <TextField label={t("config.machine.hosterEmail.label")} value={machine[key]?.email ?? ""} mono
            help={t("config.machine.hosterEmail.help")} testId={id(`${key}-email`)}
            onChange={(v) => setHoster(key, { email: v || null })} />
        </fieldset>
      ))}
    </>
  );
}
