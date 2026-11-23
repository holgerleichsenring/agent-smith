"use client";

import type { ConfigCapabilities, ProjectMerge, StudioEntity, StudioProject } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { CheckField, SelectField } from "./formFields";
import { MergeCiFields } from "./MergeCiFields";

// 2026-11-17-b34eb: requires_start (2026-11-17-9a39a) and the merge: block (2026-11-17-023fa/b). The mode lists come
// from the server's enums through capabilities, so a mode the server gains is offered without a change here. A
// project with no merge block reads as manual; the block is written only once a control here is touched.

const NO_MERGE: ProjectMerge = { when: "manual", respectBranchProtection: false, refreshOpenPullRequests: false };

export function ProjectMergeSection({
  project,
  onChange,
  capabilities,
  secrets = [],
}: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  capabilities: ConfigCapabilities | null;
  /** 2026-11-17-3435d: the catalog's secrets, for the approval identity. */
  secrets?: StudioEntity[];
}) {
  const t = useT();
  const merge = project.merge ?? NO_MERGE;
  const setMerge = (patch: Partial<ProjectMerge>) => onChange({ ...project, merge: { ...merge, ...patch } });
  return (
    <>
      <SelectField
        label={t("config.merge.requiresStart.label")}
        value={project.requiresStart ?? ""}
        options={capabilities?.requiresStart ?? []}
        placeholder={t("config.merge.requiresStart.placeholder")}
        help={t("config.merge.requiresStart.help")}
        testId="form-field-requiresStart"
        onChange={(v) => onChange({ ...project, requiresStart: v })}
      />
      <SelectField
        label={t("config.merge.when.label")}
        value={merge.when}
        options={capabilities?.mergeWhen ?? []}
        placeholder={t("config.pick")}
        help={t("config.merge.when.help")}
        testId="form-field-merge-when"
        onChange={(v) => setMerge({ when: v || "manual" })}
      />
      <CheckField
        label={t("config.merge.respectProtection.label")}
        value={merge.respectBranchProtection}
        help={t("config.merge.respectProtection.help")}
        testId="form-field-merge-respectBranchProtection"
        onChange={(v) => setMerge({ respectBranchProtection: v })}
      />
      <CheckField
        label={t("config.merge.refreshOpen.label")}
        value={merge.refreshOpenPullRequests}
        help={t("config.merge.refreshOpen.help")}
        testId="form-field-merge-refreshOpenPullRequests"
        onChange={(v) => setMerge({ refreshOpenPullRequests: v })}
      />
      <MergeCiFields merge={merge} secrets={secrets} onChange={setMerge} />
    </>
  );
}
