"use client";

import type { ConfigCapabilities, ProjectMerge, StudioProject } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { SelectField } from "./formFields";
import { MergeCiFields } from "./MergeCiFields";

// 2026-11-17-b34eb: requires_start (2026-11-17-9a39a) and the merge: block (2026-11-17-023fa/b). The mode lists come
// from the server's enums through capabilities, so a mode the server gains is offered without a change here. The merge
// block is written only once a control here is touched.
// 2026-11-25-9992i: review (end, each, none) is the one merge decision, a project-level key beside requires_start; the
// merge block keeps only the CI reading keys.

export const REVIEW_DEFAULT = "end";

const NO_MERGE: ProjectMerge = {};

export function ProjectMergeSection({
  project,
  onChange,
  capabilities,
}: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  capabilities: ConfigCapabilities | null;
}) {
  const t = useT();
  const merge = project.merge ?? NO_MERGE;
  const setMerge = (patch: Partial<ProjectMerge>) => onChange({ ...project, merge: { ...merge, ...patch } });
  return (
    <>
      <SelectField
        label={t("config.merge.review.label")}
        value={project.review || REVIEW_DEFAULT}
        options={capabilities?.review ?? []}
        placeholder={t("config.pick")}
        help={t("config.merge.review.help")}
        testId="form-field-review"
        onChange={(v) => onChange({ ...project, review: v || REVIEW_DEFAULT })}
      />
      <SelectField
        label={t("config.merge.requiresStart.label")}
        value={project.requiresStart ?? ""}
        options={capabilities?.requiresStart ?? []}
        placeholder={t("config.merge.requiresStart.placeholder")}
        help={t("config.merge.requiresStart.help")}
        testId="form-field-requiresStart"
        onChange={(v) => onChange({ ...project, requiresStart: v })}
      />
      <MergeCiFields merge={merge} onChange={setMerge} />
    </>
  );
}
