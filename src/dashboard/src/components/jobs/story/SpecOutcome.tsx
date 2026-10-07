"use client";

import { useRunSpecRecord } from "@/hooks/useRunSpecRecord";
import { ResultDocument } from "@/components/jobs/ResultTab";

// p0466: the spec the run actually executed, held by the server rather than only
// by the sandbox that produced it. Fetched when the spec is opened, so a run with
// twelve specs costs one document to read one of them.
//
// A spec with no record says so and names what was looked up — an empty pane
// would be indistinguishable from a spec whose record was never written.

export function SpecOutcome({ runId, specId }: { runId: string; specId: string }) {
  const { record, loading } = useRunSpecRecord(runId, specId);
  return (
    <div data-testid={`spec-outcome-${specId}`}>
      <h4>The spec it executed</h4>
      {record ? (
        <ResultDocument content={record} />
      ) : (
        <p className="hint" data-testid={`spec-outcome-empty-${specId}`}>
          {loading
            ? "Loading…"
            : `No executed spec recorded for ${specId} — this run wrote none, or it predates the server-held spec record.`}
        </p>
      )}
    </div>
  );
}
