"use client";

import { useRunPhaseRecord } from "@/hooks/useRunPhaseRecord";
import { ResultDocument } from "@/components/jobs/ResultTab";

// p0466: the spec the phase actually executed, held by the server rather than
// only by the sandbox that produced it. Fetched when the phase is opened, so a
// run with twelve phases costs one document to read one of them.
//
// A phase with no record says so and names what was looked up — an empty pane
// would be indistinguishable from a phase whose record was never written.

export function PhaseRecord({ runId, phaseId }: { runId: string; phaseId: string }) {
  const { record, loading } = useRunPhaseRecord(runId, phaseId);
  return (
    <div data-testid={`phase-record-${phaseId}`}>
      <h4>The spec it executed</h4>
      {record ? (
        <ResultDocument content={record} />
      ) : (
        <p className="hint" data-testid={`phase-record-empty-${phaseId}`}>
          {loading
            ? "Loading…"
            : `No executed spec recorded for ${phaseId} — this run wrote none, or it predates the server-held phase record.`}
        </p>
      )}
    </div>
  );
}
