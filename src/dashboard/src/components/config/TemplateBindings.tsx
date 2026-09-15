"use client";

import type { StudioProject, TemplateReference } from "@/lib/configApi";
import { SelectField, TextField } from "./formFields";
import { useProjectContexts } from "./useProjectContexts";
import type { ConfigCatalog } from "./useConfigCatalog";

// 2026-09-14-620e: a context of THIS project is built after a context of another
// project's repository, at a revision. Four of the five fields are PICKED — the target
// project and its repo from the catalog the studio already holds, both context names
// from what the repositories actually declare — and the revision is typed, because
// nothing in the product enumerates refs (2026-09-13-9802 verifies it where it is
// fetched). A repository that cannot be read degrades its field and never the form.

/** Where a context list comes from, said on the field rather than left to be assumed. */
const FROM_DEFAULT_BRANCH = "read from the repository's default branch";

export function TemplateBindings({
  project,
  catalog,
  onChange,
  testId = "form-templates",
}: {
  project: StudioProject;
  catalog: ConfigCatalog;
  onChange: (templates: TemplateReference[]) => void;
  testId?: string;
}) {
  const templates = project.templates ?? [];
  const local = useProjectContexts(project.id, project.repos);

  const set = (index: number, next: TemplateReference) =>
    onChange(templates.map((t, i) => (i === index ? next : t)));

  return (
    <div className="field" data-testid={testId}>
      <label>
        templates
        <span className="help">
          what each context of this project is built after
        </span>
      </label>

      {templates.length === 0 && (
        <span className="help" data-testid={`${testId}-none`}>
          this project declares no template
        </span>
      )}

      {templates.map((template, index) => (
        <TemplateRow
          key={index}
          testId={`${testId}-${index}`}
          template={template}
          localContexts={local}
          catalog={catalog}
          onChange={(next) => set(index, next)}
          onRemove={() => onChange(templates.filter((_, i) => i !== index))}
        />
      ))}

      <Unreadable testId={`${testId}-local`} subject="this project" lines={local.unreadable} />

      <div className="picks">
        <button
          type="button"
          className="pick"
          data-testid={`${testId}-add`}
          onClick={() =>
            onChange([...templates, { context: "", project: "", repo: "", templateContext: "" }])
          }
        >
          Add template
        </button>
      </div>
    </div>
  );
}

function TemplateRow({
  template,
  localContexts,
  catalog,
  onChange,
  onRemove,
  testId,
}: {
  template: TemplateReference;
  localContexts: ReturnType<typeof useProjectContexts>;
  catalog: ConfigCatalog;
  onChange: (next: TemplateReference) => void;
  onRemove: () => void;
  testId: string;
}) {
  // ProjectTemplateRules refuses a repo the target project does not carry, so the repo
  // options are that project's own refs — not the repos catalog, which is a superset.
  const target = catalog.projects.find((p) => p.id === template.project);
  const targetRepos = target?.repos ?? [];
  const targetContexts = useProjectContexts(template.project, template.repo ? [template.repo] : []);

  return (
    <div className="field" data-testid={testId}>
      <ContextField
        label="context"
        value={template.context}
        state={localContexts}
        subject="this project's repositories"
        testId={`${testId}-context`}
        onChange={(v) => onChange({ ...template, context: v })}
      />
      <SelectField
        label="built after project"
        value={template.project}
        options={catalog.projects.map((p) => p.id)}
        testId={`${testId}-project`}
        // Changing the project invalidates both the repo and the context it named.
        onChange={(v) => onChange({ ...template, project: v, repo: "", templateContext: "" })}
      />
      <SelectField
        label="its repo"
        value={template.repo}
        options={targetRepos}
        help={template.project ? undefined : "pick a project first"}
        testId={`${testId}-repo`}
        onChange={(v) => onChange({ ...template, repo: v, templateContext: "" })}
      />
      <ContextField
        label="its context"
        value={template.templateContext}
        state={targetContexts}
        subject="that repository"
        testId={`${testId}-templateContext`}
        onChange={(v) => onChange({ ...template, templateContext: v })}
      />
      <TextField
        label="revision"
        value={template.revision ?? ""}
        mono
        placeholder="a tag, a branch or a commit"
        // Typed, and said so: no source provider enumerates refs, so there is nothing to
        // pick from. The name is verified where the template is fetched.
        help="typed, not picked — nothing lists refs; it is verified where the template is fetched"
        testId={`${testId}-revision`}
        onChange={(v) => onChange({ ...template, revision: v === "" ? null : v })}
      />
      <Unreadable testId={testId} subject="the target repository" lines={targetContexts.unreadable} />
      <div className="picks">
        <button type="button" className="pick" data-testid={`${testId}-remove`} onClick={onRemove}>
          Remove template
        </button>
      </div>
    </div>
  );
}

/** Why a list is missing, never instead of the field — the name stays typeable. */
function Unreadable({
  lines,
  subject,
  testId,
}: {
  lines: string[];
  subject: string;
  testId: string;
}) {
  if (lines.length === 0) return null;
  return (
    <span className="help" data-testid={`${testId}-unreadable`}>
      could not read {subject}: {lines.join("; ")}
    </span>
  );
}

/**
 * A context name: PICKED while there is a list, TYPED when there is not. The fallback is
 * the whole point of the field degrading rather than the form — an unreachable target is
 * not a reason an operator cannot finish editing a project, and a select with no options
 * would be exactly that.
 */
function ContextField({
  label,
  value,
  state,
  subject,
  testId,
  onChange,
}: {
  label: string;
  value: string;
  state: ReturnType<typeof useProjectContexts>;
  subject: string;
  testId: string;
  onChange: (v: string) => void;
}) {
  const help = state.loading
    ? `reading ${subject}...`
    : state.names.length > 0
      ? FROM_DEFAULT_BRANCH
      : state.unreadable.length > 0
        ? "no list to pick from - type the name"
        : `${subject} declare no context - type the name`;

  return state.names.length > 0 ? (
    <SelectField label={label} value={value} options={state.names} help={help} testId={testId} onChange={onChange} />
  ) : (
    <TextField label={label} value={value} mono help={help} testId={testId} onChange={onChange} />
  );
}
