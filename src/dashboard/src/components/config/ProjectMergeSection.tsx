"use client";

import type { ConfigCapabilities, ProjectMerge, StudioEntity, StudioProject } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import { CheckField, SelectField } from "./formFields";
import { MergeCiFields } from "./MergeCiFields";

// 2026-11-17-b34eb: requires_start (2026-11-17-9a39a) and the merge: block (2026-11-17-023fa/b). The mode lists come
// from the server's enums through capabilities, so a mode the server gains is offered without a change here. A
// project with no merge block reads as manual; the block is written only once a control here is touched.
// 2026-11-24-f9c5a: series_when beside it, on_ci_green when the project says nothing.

export const SERIES_WHEN_DEFAULT = "on_ci_green";

const NO_MERGE: ProjectMerge = {
  when: "manual", respectBranchProtection: false, refreshOpenPullRequests: false, seriesWhen: SERIES_WHEN_DEFAULT,
};

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
      <SelectField
        label={t("config.merge.seriesWhen.label")}
        value={merge.seriesWhen ?? SERIES_WHEN_DEFAULT}
        options={capabilities?.mergeWhen ?? []}
        placeholder={t("config.pick")}
        help={t("config.merge.seriesWhen.help")}
        testId="form-field-merge-seriesWhen"
        onChange={(v) => setMerge({ seriesWhen: v || SERIES_WHEN_DEFAULT })}
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
