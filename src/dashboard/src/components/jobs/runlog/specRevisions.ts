import type { T } from "@/lib/i18n/translate";
import type { RunSpecDecision } from "@/lib/runSpecsApi";
import type { StepPass } from "./specPasses";

// 2026-11-26-5201g: the passes that revised a spec. revise_spec logs one decision per revision, under the source label
// SpecRevisionWriter.Source, which the decision row keeps as its reason; the row's step index is the master step that
// revised it, and specPasses says which pass that step is. The decision's own sentence names the revision and why.

/** SpecRevisionWriter.Source — the source label a revise_spec decision carries in its reason. */
export const REVISE_SPEC_SOURCE = "revise-spec";

export interface SpecRevision {
  /** The pass that revised the spec; null when the spec ran one pass or the step is not known. */
  pass: number | null;
  /** The decision as logged: "Spec … revised in run … as revision N: why". */
  text: string;
}

/** The spec's revisions, in the order they were logged. */
export function specRevisions(decisions: RunSpecDecision[], passes: ReadonlyMap<number, StepPass>): SpecRevision[] {
  return decisions
    .filter((d) => d.reason === REVISE_SPEC_SOURCE)
    .map((d) => ({ pass: d.stepIndex === null ? null : passes.get(d.stepIndex)?.number ?? null, text: d.name.trim() }));
}

/** "revised in pass 3", or "revised" when the pass is not known. */
export function revisionWords(t: T, revision: SpecRevision): string {
  return revision.pass === null ? t("runs.log.specSteps.revised") : t("runs.log.specSteps.revisedInPass", { pass: revision.pass });
}
