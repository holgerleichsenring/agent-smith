"use client";

import type { RunCallPoint, RunCommandPoint, RunSpecStatistics } from "@/lib/runStoryApi";
import { formatChars, formatMs } from "./callSeries";
import { CallSizePlot } from "./CallSizePlot";
import { CallTable } from "./CallTable";
import { CommandTable } from "./CommandTable";

// p0423b: one spec, accounted for. What it cost, what it ran and how each command ended,
// and the shape of every model call inside it. A spec that failed names the command that
// returned non-zero; a spec that stalled shows the prompt climbing while the answer fell.

export interface SpecAccountProps {
  spec: RunSpecStatistics;
  calls: RunCallPoint[];
  commands: RunCommandPoint[];
}

export function SpecAccount({ spec, calls, commands }: SpecAccountProps) {
  const failed = spec.failedCommands > 0 || spec.calls.failedCalls > 0;
  return (
    <section className="card" data-testid="spec-account" data-spec={spec.specId ?? ""}>
      <div className="card-h">
        <h3>{spec.specId ? `Spec ${spec.specId}` : "Steps outside any spec"}</h3>
        <span className={failed ? "badge bad" : "badge neu"} data-testid="spec-verdict">
          {failed ? "something did not pass" : "nothing failed"}
        </span>
      </div>
      <div className="card-b">
        <SpecNumbers spec={spec} />
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

function SpecNumbers({ spec }: { spec: RunSpecStatistics }) {
  const stats = spec.calls;
  return (
    <div className="health health-plain" data-testid="spec-numbers">
      <Metric label="Steps" value={String(spec.steps)} />
      <Metric label="Took" value={formatMs(spec.durationMs)} />
      <Metric
        label="Calls"
        value={String(stats.calls)}
        note={stats.failedCalls > 0 ? `${stats.failedCalls} did not end well` : undefined}
      />
      <Metric label="Largest prompt" value={formatChars(stats.largestPromptChars)} />
      <Metric label="Smallest answer" value={formatChars(stats.smallestResponseChars)} />
      <Metric
        label="Commands"
        value={String(spec.commands)}
        note={spec.failedCommands > 0 ? `${spec.failedCommands} non-zero` : undefined}
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
