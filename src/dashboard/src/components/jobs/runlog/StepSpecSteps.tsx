"use client";

import type { ProgressLedgerEntry } from "@/types/hub-events";
import { InlineText } from "./LogText";
import { StepPart } from "./StepPart";
import { SPEC_STEP_WORDS, specStepsCount } from "./specStepLines";
import { useSpecStepLines, type GivenPasses } from "./useSpecStepLines";

// 2026-11-19-1b72d: the opened spec step's first part — the spec's steps, numbered, each done, in progress or open.

interface Props {
  runId: string;
  specId: string;
  ledger: ProgressLedgerEntry[] | null;
  ledgerIsThisSpec: boolean;
  /** The run's passes as the page already read them; absent, they are read here. */
  passes?: GivenPasses;
}

export function StepSpecSteps({ runId, specId, ledger, ledgerIsThisSpec, passes }: Props) {
  const { lines, loading, stepsFailed, statesFailed } = useSpecStepLines(runId, specId, ledger, ledgerIsThisSpec, passes);
  return (
    <StepPart title={lines.length > 0 && !statesFailed ? `Steps of the spec · ${specStepsCount(lines)}` : "Steps of the spec"} testId="step-part-steps">
      {lines.map((l) => (
        <div key={l.id} className="rl-part-row rl-step-of-spec" data-state={statesFailed ? "unknown" : l.state} data-testid="step-spec-step">
          <span className="rl-part-dot" aria-hidden="true" />
          <span className="rl-part-key">{l.number}</span>
          <span className="rl-part-what"><InlineText text={l.text} /></span>
          <span className="rl-part-state">{statesFailed ? "" : SPEC_STEP_WORDS[l.state]}</span>
        </div>
      ))}
      {lines.length > 0 && statesFailed && (
        <div className="rl-part-row rl-part-note rl-bad" data-testid="step-spec-states-failed">the steps&rsquo; states could not be read</div>
      )}
      {lines.length === 0 && (
        <div className={`rl-part-row rl-part-note ${stepsFailed ? "rl-bad" : "rl-dim"}`} data-testid="step-spec-steps-empty">
          {loading ? "reading the spec…" : stepsFailed ? "the spec's steps could not be read" : "no steps recorded for this spec"}
        </div>
      )}
    </StepPart>
  );
}
