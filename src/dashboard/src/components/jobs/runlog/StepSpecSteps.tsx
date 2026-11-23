"use client";

import { useT } from "@/hooks/useT";
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
  const t = useT();
  const { lines, loading, stepsFailed, statesFailed } = useSpecStepLines(runId, specId, ledger, ledgerIsThisSpec, passes);
  return (
    <StepPart title={lines.length > 0 && !statesFailed ? t("runs.log.part.specStepsCount", { count: specStepsCount(t, lines) }) : t("runs.log.part.specSteps")} testId="step-part-steps">
      {lines.map((l) => (
        <div key={l.id} className="rl-part-row rl-step-of-spec" data-state={statesFailed ? "unknown" : l.state} data-testid="step-spec-step">
          <span className="rl-part-dot" aria-hidden="true" />
          <span className="rl-part-key">{l.number}</span>
          <span className="rl-part-what"><InlineText text={l.text} /></span>
          <span className="rl-part-state">{statesFailed ? "" : t(SPEC_STEP_WORDS[l.state])}</span>
        </div>
      ))}
      {lines.length > 0 && statesFailed && (
        <div className="rl-part-row rl-part-note rl-bad" data-testid="step-spec-states-failed">{t("runs.log.specSteps.statesFailed")}</div>
      )}
      {lines.length === 0 && (
        <div className={`rl-part-row rl-part-note ${stepsFailed ? "rl-bad" : "rl-dim"}`} data-testid="step-spec-steps-empty">
          {t(loading ? "runs.log.specSteps.reading" : stepsFailed ? "runs.log.specSteps.failed" : "runs.log.specSteps.none")}
        </div>
      )}
    </StepPart>
  );
}
