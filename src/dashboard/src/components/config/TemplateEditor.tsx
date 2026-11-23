"use client";

import type { TemplateReference } from "@/lib/configApi";
import { SelectField, TextField } from "./formFields";
import { useProjectContexts } from "./useProjectContexts";
import { ContextField, LocalContextField, Unreadable } from "./templateContextField";
import type { ConfigCatalog } from "./useConfigCatalog";
import { DeploymentEnvironments } from "./DeploymentEnvironments";
import { useT } from "@/hooks/useT";


// 2026-11-18-3863a: a binding is a code template (how the context is built) or a deployment template (how it is
// delivered, with its environments).
const TEMPLATE_KINDS = [
  { value: "code", label: "config.template.kind.code" },
  { value: "deployment", label: "config.template.kind.deployment" },
] as const;

// 2026-09-14-620e: the five fields of one template binding. Four are PICKED — the target
// project and its repo from the catalog the studio already holds, both context names from
// what the repositories actually declare — and the revision is typed, because nothing in
// the product enumerates refs (2026-09-13-9802 verifies it where it is fetched).
//
// 2026-09-15-a2d0: they render only for the row the operator opened, which is also what
// bounds the outbound read below to one repository at a time.

export function TemplateEditor({
  template,
  localContexts,
  catalog,
  onChange,
  testId,
}: {
  template: TemplateReference;
  localContexts: ReturnType<typeof useProjectContexts>;
  catalog: ConfigCatalog;
  onChange: (next: TemplateReference) => void;
  testId: string;
}) {
  // ProjectTemplateRules refuses a repo the target project does not carry, so the repo
  // options are that project's own refs — not the repos catalog, which is a superset.
  const t = useT();
  const target = catalog.projects.find((p) => p.id === template.project);
  const targetRepos = target?.repos ?? [];
  // One authenticated read into the target repository, made while this row is open. A
  // closed row makes none, and re-opening reads again: nothing caches this call.
  const targetContexts = useProjectContexts(template.project, template.repo ? [template.repo] : []);

  return (
    <div className="tpl-editor" id={`${testId}-editor`} data-testid={`${testId}-editor`}>
      <SelectField
        label={t("config.template.kind.label")}
        value={template.kind ?? "code"}
        options={TEMPLATE_KINDS.map((k) => ({ value: k.value, label: t(k.label) }))}
        testId={`${testId}-kind`}
        // A code binding carries no environments; leaving deployment drops them.
        onChange={(v) => onChange({ ...template, kind: v === "deployment" ? v : null,
          environments: v === "deployment" ? template.environments ?? [] : null })}
      />
      <LocalContextField
        value={template.context}
        contextRepo={template.contextRepo ?? null}
        state={localContexts}
        testId={`${testId}-context`}
        onChange={(name, repo) => onChange({ ...template, context: name, contextRepo: repo })}
      />
      <SelectField
        label={t("config.template.project.label")}
        value={template.project}
        options={catalog.projects.map((p) => p.id)}
        testId={`${testId}-project`}
        // Changing the project invalidates both the repo and the context it named.
        onChange={(v) => onChange({ ...template, project: v, repo: "", templateContext: "" })}
      />
      <SelectField
        label={t("config.template.repo.label")}
        value={template.repo}
        options={targetRepos}
        help={template.project ? undefined : t("config.template.repo.pickProject")}
        testId={`${testId}-repo`}
        onChange={(v) => onChange({ ...template, repo: v, templateContext: "" })}
      />
      <ContextField
        label={t("config.template.context.label")}
        value={template.templateContext}
        state={targetContexts}
        subject={t("config.template.context.subject")}
        testId={`${testId}-templateContext`}
        onChange={(v) => onChange({ ...template, templateContext: v })}
      />
      <TextField
        label={t("config.template.revision.label")}
        value={template.revision ?? ""}
        mono
        placeholder={t("config.template.revision.placeholder")}
        // Typed, and said so: no source provider enumerates refs, so there is nothing to
        // pick from. The name is verified where the template is fetched.
        help={t("config.template.revision.help")}
        testId={`${testId}-revision`}
        onChange={(v) => onChange({ ...template, revision: v === "" ? null : v })}
      />
      {template.kind === "deployment" && (
        <DeploymentEnvironments environments={template.environments ?? []} catalog={catalog} testId={testId}
          onChange={(environments) => onChange({ ...template, environments })} />
      )}
      <Unreadable testId={testId} subject={t("config.template.targetRepo")} lines={targetContexts.unreadable} />
    </div>
  );
}
