"use client";

import type { DeploymentEnvironmentReference } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { CheckField, MapField, SelectField, TextField } from "./formFields";
import type { ConfigCatalog } from "./useConfigCatalog";

// 2026-11-18-3863a: the environments of a deployment binding, one block per environment. The name is the template's
// word for it; the repo comes from the repos catalog (not this project's repos, so coding runs never check it out);
// approval on means a person merges the delivery's pull request; parameters are the template's, as strings.

const BLANK: DeploymentEnvironmentReference = { name: "", repo: null, approval: null, parameters: null };

export function DeploymentEnvironments({
  environments,
  catalog,
  onChange,
  testId,
}: {
  environments: DeploymentEnvironmentReference[];
  catalog: ConfigCatalog;
  onChange: (next: DeploymentEnvironmentReference[]) => void;
  testId: string;
}) {
  const t = useT();
  const set = (index: number, next: DeploymentEnvironmentReference) =>
    onChange(environments.map((e, i) => (i === index ? next : e)));
  return (
    <div className="field" data-testid={`${testId}-environments`}>
      <label>
        {t("config.environments.title")}
        <span className="help">{t("config.environments.help")}</span>
      </label>
      {environments.map((environment, index) => (
        <EnvironmentBlock
          key={index}
          environment={environment}
          repos={catalog.repos.map((r) => r.id)}
          testId={`${testId}-env-${index}`}
          onChange={(next) => set(index, next)}
          onRemove={() => onChange(environments.filter((_, i) => i !== index))}
        />
      ))}
      <div className="picks">
        <button type="button" className="pick" data-testid={`${testId}-env-add`}
          onClick={() => onChange([...environments, { ...BLANK }])}>
          {t("config.environments.add")}
        </button>
      </div>
    </div>
  );
}

function EnvironmentBlock({
  environment,
  repos,
  testId,
  onChange,
  onRemove,
}: {
  environment: DeploymentEnvironmentReference;
  repos: string[];
  testId: string;
  onChange: (next: DeploymentEnvironmentReference) => void;
  onRemove: () => void;
}) {
  const t = useT();
  return (
    <div className="tpl-env" data-testid={testId}>
      <TextField label={t("config.environments.name")} value={environment.name} mono required placeholder="test" testId={`${testId}-name`}
        onChange={(v) => onChange({ ...environment, name: v })} />
      <SelectField label={t("config.environments.repo.label")} value={environment.repo ?? ""} options={repos}
        placeholder={t("config.environments.repo.placeholder")} testId={`${testId}-repo`}
        onChange={(v) => onChange({ ...environment, repo: v === "" ? null : v })} />
      <CheckField label={t("config.environments.approval.label")} value={environment.approval ?? true} testId={`${testId}-approval`}
        help={t("config.environments.approval.help")}
        onChange={(v) => onChange({ ...environment, approval: v })} />
      <MapField label={t("config.environments.parameters.label")} values={environment.parameters ?? {}} testId={`${testId}-parameters`}
        help={t("config.environments.parameters.help")}
        onChange={(v) => onChange({ ...environment, parameters: v ?? null })} />
      <button type="button" className="pick" aria-label={t("config.environments.removeAria", { name: environment.name || "" }).trim()}
        data-testid={`${testId}-remove`} onClick={onRemove}>
        {t("config.environments.remove")}
      </button>
    </div>
  );
}
