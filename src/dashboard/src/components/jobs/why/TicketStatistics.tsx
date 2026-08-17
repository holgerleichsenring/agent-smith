"use client";

import type { RunStatistics } from "@/lib/runStoryApi";
import type { RunAcceptance } from "@/types/hub-events";
import { formatChars, formatMs } from "./callSeries";

// p0423b: what the ticket cost, read off the trail. Phases, criteria, duration per ticket,
// calls and their sizes — every one of them a fold over the recorded events, never a
// counter kept beside them. A counter and the events it counts are two answers to one
// question, and they drift.

export interface TicketStatisticsProps {
  statistics: RunStatistics;
  acceptance: RunAcceptance | null;
}

export function TicketStatistics({ statistics, acceptance }: TicketStatisticsProps) {
  const totals = statistics.totals;
  const criteria = acceptance?.criteria ?? [];
  const met = criteria.filter((c) => c.status === "met").length;

  return (
    <section className="card" data-testid="ticket-statistics">
      <div className="card-h">
        <h3>What this ticket cost</h3>
        <span className="badge neu">derived from the trail</span>
      </div>
      <div className="card-b">
        <div className="health health-plain">
          <Metric label="Phases" value={String(statistics.phases.length)} />
          <Metric
            label="Criteria"
            value={criteria.length > 0 ? `${met}/${criteria.length}` : "—"}
            note={criteria.length > 0 ? "proven" : "none ratified"}
          />
          <Metric label="Wall clock" value={formatMs(statistics.totalDurationMs)} />
          <Metric
            label="Calls"
            value={String(totals.calls)}
            note={totals.failedCalls > 0 ? `${totals.failedCalls} did not end well` : undefined}
          />
          <Metric label="Largest prompt" value={formatChars(totals.largestPromptChars)} />
          <Metric label="Smallest answer" value={formatChars(totals.smallestResponseChars)} />
          <Metric label="Time in calls" value={formatMs(totals.totalDurationMs)} />
          <Metric label="Tool calls" value={String(totals.toolCalls)} />
          <Metric
            label="Never delivered"
            value={formatChars(totals.toolCharsNeverDelivered)}
            note="tool output a bound cut"
          />
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
