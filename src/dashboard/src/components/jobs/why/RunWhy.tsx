"use client";

import Link from "next/link";
import { useRunStatistics } from "@/hooks/useRunStatistics";
import { useRunDetailSnapshot } from "@/hooks/useRunDetailSnapshot";
import { VerifySummary } from "@/components/jobs/story/VerifySummary";
import { buildVerifyFallback } from "@/components/jobs/story/verifyFallback";
import { TicketStatistics } from "./TicketStatistics";
import { PhaseAccount } from "./PhaseAccount";
import { TraceReader } from "./TraceReader";

// p0423b: THE STORY VIEW — why did this run do that. It is opened deliberately, per run,
// and it is the only screen carrying statistics: the live view answers "what is happening"
// and shows progress. Noise is a property of the view, not of the store, so everything
// cheap is recorded and almost nothing is shown until somebody asks this question.
//
// Read top to bottom it is one argument: what the ticket cost, what it promised and how
// each promise was accounted for, then phase by phase the commands with their exit codes
// and the calls with their sizes — and, when the run was traced, the conversation itself.

export function RunWhy({ runId }: { runId: string }) {
  const snapshot = useRunDetailSnapshot(runId, null);
  const { statistics, loading, error } = useRunStatistics(runId);

  return (
    <div className="mock-shell mock-viewer">
      <main className="wrap" data-testid="run-why-root">
        <div className="m-head">
          <div>
            <h1>Why this run did that</h1>
            <div className="msub">
              {snapshot?.ticketId ? `${snapshot.ticketId} · ` : ""}
              <span className="mono">{runId}</span>
            </div>
          </div>
          <Link className="trace-btn" href={`/jobs/${encodeURIComponent(runId)}`} data-testid="run-why-back">
            ← Back to the run
          </Link>
        </div>

        {error && (
          <p className="hint" data-testid="run-why-error">
            The run&rsquo;s record could not be read: {error}
          </p>
        )}
        {loading && !statistics && <p className="hint">Reading the run&rsquo;s record…</p>}

        {statistics && (
          <div className="stage">
            <TicketStatistics statistics={statistics} acceptance={snapshot?.acceptance ?? null} />

            {snapshot?.acceptance ? (
              <VerifySummary
                acceptance={snapshot.acceptance}
                fallback={buildVerifyFallback([])}
              />
            ) : (
              <p className="hint" data-testid="run-why-no-acceptance">
                No acceptance criteria were ratified on this run, so there is nothing to
                account for against.
              </p>
            )}

            {statistics.phases.length === 0 ? (
              <p className="hint" data-testid="run-why-no-phases">
                This run recorded no step, so there is no phase to account for. Runs older
                than the durable record can look like this.
              </p>
            ) : (
              statistics.phases.map((phase) => (
                <PhaseAccount
                  key={phase.phaseId ?? "unphased"}
                  phase={phase}
                  calls={statistics.calls.filter((c) => c.phaseId === phase.phaseId)}
                  commands={statistics.commands.filter((c) => c.phaseId === phase.phaseId)}
                />
              ))
            )}

            <section className="card" data-testid="run-why-trace">
              <div className="card-h">
                <h3>The recorded conversation</h3>
              </div>
              <div className="card-b">
                <TraceReader runId={runId} />
              </div>
            </section>
          </div>
        )}
      </main>
    </div>
  );
}
