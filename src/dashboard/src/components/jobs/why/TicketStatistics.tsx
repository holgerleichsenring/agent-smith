"use client";

import type { RunStatistics } from "@/lib/runStoryApi";
import type { RunAcceptance } from "@/types/hub-events";
import { formatChars, formatMs } from "./callSeries";
import { WorkBreakdown } from "./WorkBreakdown";

// p0423b: what the ticket cost, read off the trail. Phases, criteria, duration per ticket,
// calls and their sizes — every one of them a fold over the recorded events, never a
// counter kept beside them. A counter and the events it counts are two answers to one
// question, and they drift.

export interface TicketStatisticsProps {
  statistics: RunStatistics;
  acceptance: RunAcceptance | null;
  /** p0341h: a running run has not been accounted for yet, which is not the same as having
   *  nothing to account for. Saying "none ratified" mid-run reads as a verdict. */
  running?: boolean;
}

export function TicketStatistics({ statistics, acceptance, running }: TicketStatisticsProps) {
  const totals = statistics.totals;
  const criteria = acceptance?.criteria ?? [];
  const met = criteria.filter((c) => c.status === "met").length;
  const work = statistics.work;

  return (
    <section className="card" data-testid="ticket-statistics">
      <div className="card-h">
        <h3>What this ticket cost</h3>
        <span className="badge neu">derived from the trail</span>
      </div>
      <div className="card-b">
        {work && (
          <>
            <WorkBreakdown
              title="Pipeline — what the run did"
              subtitle="steps"
              kinds={work.pipeline}
              testId="work-pipeline"
            />
            <WorkBreakdown
              title="Sandbox — how it did it"
              subtitle="commands"
              kinds={work.sandbox}
              testId="work-sandbox"
            />
          </>
        )}
        <div className="health health-plain">
          <Metric label="Phases" value={String(statistics.phases.length)} />
          <Metric
            label="Criteria"
            value={criteria.length > 0 ? `${met}/${criteria.length}` : "—"}
            note={
              criteria.length > 0 ? "proven" : running ? "not accounted for yet" : "none ratified"
            }
          />
          {/* p0341h: the sum of the steps is not the wall clock — sandboxes run in parallel,
              so on run a98c these were 39m against 65m of elapsed time. The run header owns
              the wall clock; this panel says exactly what it measured. */}
          <Metric label="Time in steps" value={formatMs(statistics.totalDurationMs)} />
          <Metric
            label="Calls"
            value={String(totals.calls)}
            note={totals.failedCalls > 0 ? `${totals.failedCalls} did not end well` : undefined}
          />
          <Metric label="Largest prompt" value={formatChars(totals.largestPromptChars)} />
          <Metric label="Time in calls" value={formatMs(totals.totalDurationMs)} />
          {/* p0341h: shown only when it HAPPENED. A metric that can only ever read zero —
              because the events feeding it never reach the trail — teaches a reader to stop
              looking at the panel. A real cut is worth a row. */}
          {totals.toolCharsNeverDelivered > 0 && (
            <Metric
              label="Never delivered"
              value={formatChars(totals.toolCharsNeverDelivered)}
              note="tool output a bound cut"
            />
          )}
          <Metric label="Retries" value={String(totals.retries)} />
        </div>
        {statistics.truncated && (
          <p className="hint" data-testid="ticket-statistics-truncated">
            This run produced more calls or commands than one page carries — the series below
            show the run&rsquo;s most recent ones. The totals cover the whole run.
          </p>
        )}
      </div>
    </section>
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
