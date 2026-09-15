"use client";

import { useState } from "react";
import type { StudioProject, TemplateReference } from "@/lib/configApi";
import { useProjectContexts } from "./useProjectContexts";
import { Unreadable } from "./templateContextField";
import { TemplateRow } from "./TemplateRow";
import type { ConfigCatalog } from "./useConfigCatalog";

// 2026-09-14-620e: a context of THIS project is built after a context of another
// project's repository, at a revision.
//
// 2026-09-15-a2d0: the bindings are a LIST, not five stacked fields per declaration. Three
// templates were fifteen fields in a 520px drawer with no line saying where one binding
// ended, so the values stay on the rows and the fields open for one row at a time. The
// single stored declaration — the only shape on disk today — is that row on load.

const BLANK: TemplateReference = { context: "", project: "", repo: "", templateContext: "" };

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
  // The open row is a VIEW, never a write: the value stays project.templates, so
  // collapsing cannot lose a half-finished binding. One row at a time, and the index is
  // safe to hold because every removal closes the editor (below) — the alternative is
  // MapField's generated row id, which buys nothing once nothing stays open across a
  // removal.
  // Reaching another project means closing the drawer — ConfigStudio renders
  // {drawer && <EntityDrawer/>} and the scrim covers the cards — so this unmounts between
  // projects and re-initialises by construction. A guard keyed on project.id would be
  // worse than nothing: the id field is editable while isNew, so it would re-open a row
  // the operator just closed on every keystroke.
  const [open, setOpen] = useState<number | null>(templates.length === 1 ? 0 : null);

  const set = (index: number, next: TemplateReference) =>
    onChange(templates.map((t, i) => (i === index ? next : t)));

  const remove = (index: number) => {
    setOpen(null);
    onChange(templates.filter((_, i) => i !== index));
  };

  const add = () => {
    setOpen(templates.length);
    onChange([...templates, { ...BLANK }]);
  };

  return (
    <div className="field" data-testid={testId}>
      <label>
        templates
        <span className="help">
          what each context of this project is built after
          {templates.length > 0 && ` — ${templates.length} declared`}
        </span>
      </label>

      {templates.length === 0 && (
        <span className="help" data-testid={`${testId}-none`}>
          this project declares no template
        </span>
      )}

      {/* Declaration order, never sorted: the resolver preserves it and the consumers
          that open and read a template iterate it in that order. */}
      {templates.length > 0 && (
        <div className="tpl-rows" role="list">
          {/* key={index} is safe only because of the invariant above it: remove closes the
              editor BEFORE the array changes, add appends at the tail, and an edit never
              changes the length — so no reconciled subtree carries a mounted editor, and
              its in-flight read, across a reindex. Anything that reorders breaks that. */}
          {templates.map((template, index) => (
            <TemplateRow
              key={index}
              testId={`${testId}-${index}`}
              index={index}
              template={template}
              open={open === index}
              localContexts={local}
              catalog={catalog}
              onToggle={() => setOpen(open === index ? null : index)}
              onChange={(next) => set(index, next)}
              onRemove={() => remove(index)}
            />
          ))}
        </div>
      )}

      <Unreadable testId={`${testId}-local`} subject="this project" lines={local.unreadable} />

      <div className="picks">
        <button type="button" className="pick" data-testid={`${testId}-add`} onClick={add}>
          Add template
        </button>
      </div>
    </div>
  );
}
