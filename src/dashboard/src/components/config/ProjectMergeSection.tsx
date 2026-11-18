"use client";

import type { ConfigCapabilities, ProjectMerge, StudioEntity, StudioProject } from "@/lib/configApi";
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
  const merge = project.merge ?? NO_MERGE;
  const setMerge = (patch: Partial<ProjectMerge>) => onChange({ ...project, merge: { ...merge, ...patch } });
  return (
    <>
      <SelectField
        label="requires start"
        value={project.requiresStart ?? ""}
        options={capabilities?.requiresStart ?? []}
        placeholder="— default (on_green_push) —"
        help="when a ticket that requires another may start: once each prerequisite pushed a green branch, or once each merged"
        testId="form-field-requiresStart"
        onChange={(v) => onChange({ ...project, requiresStart: v })}
      />
      <SelectField
        label="merge when"
        value={merge.when}
        options={capabilities?.mergeWhen ?? []}
        placeholder="— pick —"
        help="whether a green code run merges its own pull requests"
        testId="form-field-merge-when"
        onChange={(v) => setMerge({ when: v || "manual" })}
      />
      <CheckField
        label="respect branch protection"
        value={merge.respectBranchProtection}
        help="on: arm the host's auto-complete or auto-merge and let its rules decide; off: merge past them, which needs the token's bypass permission"
        testId="form-field-merge-respectBranchProtection"
        onChange={(v) => setMerge({ respectBranchProtection: v })}
      />
      <CheckField
        label="refresh open pull requests"
        value={merge.refreshOpenPullRequests}
        help="when a ticket lands, merge its base into the other open agent-smith pull requests on that repository and base"
        testId="form-field-merge-refreshOpenPullRequests"
        onChange={(v) => setMerge({ refreshOpenPullRequests: v })}
      />
      <MergeCiFields merge={merge} secrets={secrets} onChange={setMerge} />
    </>
  );
}
