"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import { useRunPhases } from "@/hooks/useRunPhases";
import type { RunPhaseRow } from "@/lib/runPhasesApi";
import { PhaseRecord } from "./PhaseRecord";

// p0466: the Building beat as a list of PHASES you can open. A run cut into
// phases did its work one phase at a time, and each one produced decisions,
// steps and the spec it executed — all of which used to exist only while the
// phase was live. Opening a finished phase shows what it decided and what it
// was asked to do; nothing here is derived from a step-name prefix.
//
// A run with no derived phases renders nothing at all: the beat then looks
// exactly as it did before, rather than showing an empty segment.

const BADGE: Record<string, { cls: string; label: string }> = {
  done: { cls: "ok", label: "done" },
  in_progress: { cls: "run", label: "in progress" },
  failed: { cls: "bad", label: "failed" },
  not_started: { cls: "neu", label: "not started" },
};

export function PhasesPanel({ runId, revision }: { runId: string; revision: unknown }) {
  const phases = useRunPhases(runId, revision);
  const [open, setOpen] = useState<string | null>(null);
  if (phases.length === 0) return null;
  return (
    <section className="card" data-testid="phases-panel">
      <div className="card-h">
        <h3>Phases</h3>
        <span className="badge neu">{phases.length}</span>
      </div>
      <div className="card-b">
        {phases.map((phase) => (
          <PhaseSegment
            key={phase.phaseId}
            runId={runId}
            phase={phase}
            open={open === phase.phaseId}
            onToggle={() => setOpen(open === phase.phaseId ? null : phase.phaseId)}
          />
        ))}
      </div>
    </section>
  );
}

function PhaseSegment({
  runId,
  phase,
  open,
  onToggle,
}: {
  runId: string;
  phase: RunPhaseRow;
  open: boolean;
  onToggle: () => void;
}) {
  const badge = BADGE[phase.status] ?? BADGE.not_started;
  return (
    <div className="note-row" data-testid={`phase-${phase.phaseId}`}>
      <div className="body">
        <button
          type="button"
          className="w-full text-left"
          aria-expanded={open}
          data-testid={`phase-toggle-${phase.phaseId}`}
          onClick={onToggle}
        >
          <span className="file">{phase.phaseId}</span> — {phase.title}{" "}
          <span className={cn("badge", badge.cls)}>{badge.label}</span>
        </button>
        <div className="w" data-testid={`phase-meta-${phase.phaseId}`}>
          {`${phase.steps.length} step(s) · ${phase.decisions.length} decision(s)`}
          {phase.verdict ? ` · ${phase.verdict}` : ""}
        </div>
        {open && <PhaseBody runId={runId} phase={phase} />}
      </div>
    </div>
  );
}

function PhaseBody({ runId, phase }: { runId: string; phase: RunPhaseRow }) {
  return (
    <div data-testid={`phase-body-${phase.phaseId}`}>
      <h4>Decisions</h4>
      {phase.decisions.length > 0 ? (
        <ul data-testid={`phase-decisions-${phase.phaseId}`}>
          {phase.decisions.map((d, i) => (
            <li key={`${d.name}-${i}`}>
              {d.name}
              {d.reason ? ` — ${d.reason}` : ""}
            </li>
          ))}
        </ul>
      ) : (
        <p className="hint" data-testid={`phase-no-decisions-${phase.phaseId}`}>
          No decision was logged in this phase.
        </p>
      )}
      <PhaseRecord runId={runId} phaseId={phase.phaseId} />
    </div>
  );
}
