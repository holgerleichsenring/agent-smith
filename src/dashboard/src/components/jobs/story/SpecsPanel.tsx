"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";
import { useRunSpecs } from "@/hooks/useRunSpecs";
import type { RunSpecRow } from "@/lib/runSpecsApi";
import { SpecOutcome } from "./SpecOutcome";

// p0466: the Building beat as a list of SPECS you can open. A run that executes
// specs does its work one spec at a time, and each one produced decisions, steps
// and the spec body it executed — all of which used to exist only while the spec
// was live. Opening a finished spec shows what it decided and what it was asked
// to do; nothing here is derived from a step-name prefix.
//
// A run that executed no spec renders nothing at all: the beat then looks
// exactly as it did before, rather than showing an empty segment.

const BADGE: Record<string, { cls: string; label: string }> = {
  done: { cls: "ok", label: "done" },
  in_progress: { cls: "run", label: "in progress" },
  failed: { cls: "bad", label: "failed" },
  // 2026-09-17-0e79c: a spec handed back on a false premise was never built. Falling
  // through to the not_started default would have said so of a spec the run stopped on.
  handed_back: { cls: "bad", label: "handed back" },
  not_started: { cls: "neu", label: "not started" },
};

export function SpecsPanel({ runId, revision }: { runId: string; revision: unknown }) {
  const specs = useRunSpecs(runId, revision);
  const [open, setOpen] = useState<string | null>(null);
  if (specs.length === 0) return null;
  return (
    <section className="card" data-testid="specs-panel">
      <div className="card-h">
        <h3>Specs</h3>
        <span className="badge neu">{specs.length}</span>
      </div>
      <div className="card-b">
        {specs.map((spec) => (
          <SpecSegment
            key={spec.specId}
            runId={runId}
            spec={spec}
            open={open === spec.specId}
            onToggle={() => setOpen(open === spec.specId ? null : spec.specId)}
          />
        ))}
      </div>
    </section>
  );
}

function SpecSegment({
  runId,
  spec,
  open,
  onToggle,
}: {
  runId: string;
  spec: RunSpecRow;
  open: boolean;
  onToggle: () => void;
}) {
  const badge = BADGE[spec.status] ?? BADGE.not_started;
  return (
    <div className="note-row" data-testid={`spec-${spec.specId}`}>
      <div className="body">
        <button
          type="button"
          className="w-full text-left"
          aria-expanded={open}
          data-testid={`spec-toggle-${spec.specId}`}
          onClick={onToggle}
        >
          <span className="file">{spec.specId}</span> — {spec.title}{" "}
          <span className={cn("badge", badge.cls)}>{badge.label}</span>
        </button>
        <div className="w" data-testid={`spec-meta-${spec.specId}`}>
          {`${spec.steps.length} step(s) · ${spec.decisions.length} decision(s)`}
          {spec.verdict ? ` · ${spec.verdict}` : ""}
        </div>
        {open && <SpecBody runId={runId} spec={spec} />}
      </div>
    </div>
  );
}

function SpecBody({ runId, spec }: { runId: string; spec: RunSpecRow }) {
  return (
    <div data-testid={`spec-body-${spec.specId}`}>
      <h4>Decisions</h4>
      {spec.decisions.length > 0 ? (
        <ul data-testid={`spec-decisions-${spec.specId}`}>
          {spec.decisions.map((d, i) => (
            <li key={`${d.name}-${i}`}>
              {d.name}
              {d.reason ? ` — ${d.reason}` : ""}
            </li>
          ))}
        </ul>
      ) : (
        <p className="hint" data-testid={`spec-no-decisions-${spec.specId}`}>
          No decision was logged in this spec.
        </p>
      )}
      <SpecOutcome runId={runId} specId={spec.specId} />
    </div>
  );
}
