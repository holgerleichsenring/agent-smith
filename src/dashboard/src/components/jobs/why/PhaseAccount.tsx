"use client";

import type { RunCallPoint, RunCommandPoint, RunPhaseStatistics } from "@/lib/runStoryApi";
import { formatChars, formatMs } from "./callSeries";
import { CallSizePlot } from "./CallSizePlot";
import { CallTable } from "./CallTable";
import { CommandTable } from "./CommandTable";

// p0423b: one phase, accounted for. What it cost, what it ran and how each command ended,
// and the shape of every model call inside it. A phase that failed names the command that
// returned non-zero; a phase that stalled shows the prompt climbing while the answer fell.

export interface PhaseAccountProps {
  phase: RunPhaseStatistics;
  calls: RunCallPoint[];
  commands: RunCommandPoint[];
}

export function PhaseAccount({ phase, calls, commands }: PhaseAccountProps) {
  const failed = phase.failedCommands > 0 || phase.calls.failedCalls > 0;
  return (
    <section className="card" data-testid="phase-account" data-phase={phase.phaseId ?? ""}>
      <div className="card-h">
        <h3>{phase.phaseId ? `Phase ${phase.phaseId}` : "Steps outside any phase"}</h3>
        <span className={failed ? "badge bad" : "badge neu"} data-testid="phase-verdict">
          {failed ? "something did not pass" : "nothing failed"}
        </span>
      </div>
      <div className="card-b">
        <PhaseNumbers phase={phase} />
        <h4 style={HEADING}>Calls — prompt against answer, in call order</h4>
        <CallSizePlot calls={calls} />
        <h4 style={HEADING}>Commands and how they ended</h4>
        <CommandTable commands={commands} />
        <h4 style={HEADING}>The calls in full</h4>
        <CallTable calls={calls} />
      </div>
    </section>
  );
}

const HEADING: React.CSSProperties = {
  fontSize: "11px",
  letterSpacing: "0.1em",
  textTransform: "uppercase",
  color: "var(--ink-3)",
  fontWeight: 600,
  margin: "18px 0 6px",
};

function PhaseNumbers({ phase }: { phase: RunPhaseStatistics }) {
  const stats = phase.calls;
  return (
    <div className="health health-plain" data-testid="phase-numbers">
      <Metric label="Steps" value={String(phase.steps)} />
      <Metric label="Took" value={formatMs(phase.durationMs)} />
      <Metric
        label="Calls"
        value={String(stats.calls)}
        note={stats.failedCalls > 0 ? `${stats.failedCalls} did not end well` : undefined}
      />
      <Metric label="Largest prompt" value={formatChars(stats.largestPromptChars)} />
      <Metric label="Smallest answer" value={formatChars(stats.smallestResponseChars)} />
      <Metric
        label="Commands"
        value={String(phase.commands)}
        note={phase.failedCommands > 0 ? `${phase.failedCommands} non-zero` : undefined}
      />
      <Metric
        label="Tool output"
        value={formatChars(stats.toolOutputChars)}
        note={
          stats.toolCharsNeverDelivered > 0
            ? `${formatChars(stats.toolCharsNeverDelivered)} never reached the model`
            : undefined
        }
      />
      <Metric label="Retries" value={String(stats.retries)} />
    </div>
  );
}

function Metric({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="metric">
      <span className="k">{label}</span>
      <span className="v num">
        {value}
        {note && <small> {note}</small>}
      </span>
    </div>
  );
}
