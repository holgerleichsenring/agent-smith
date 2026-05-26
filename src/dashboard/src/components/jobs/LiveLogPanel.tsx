"use client";

import { useJobStream } from "@/hooks/useJobStream";
import { ProgressBar } from "./ProgressBar";
import { ToolCallEntry } from "./ToolCallEntry";
import { ObservationCard } from "./ObservationCard";
import type { JobStreamEvent, ProgressEvent } from "@/types/job-stream-events";

function lastProgress(events: JobStreamEvent[]): ProgressEvent | null {
  for (let i = events.length - 1; i >= 0; i--) {
    if (events[i].type === "progress") return events[i] as ProgressEvent;
  }
  return null;
}

export function LiveLogPanel({
  jobId,
  fromBeginning = false,
}: {
  jobId: string;
  fromBeginning?: boolean;
}) {
  const { events, reconnecting, status } = useJobStream(jobId, { fromBeginning });
  const progress = lastProgress(events);

  return (
    <section data-testid="live-log-panel" className="space-y-3">
      {reconnecting && (
        <div data-testid="reconnect-alert" className="rounded-md border border-amber-300 bg-amber-50 px-3 py-2 text-xs">
          Reconnecting to live stream…
        </div>
      )}
      {progress && <ProgressBar step={progress.step} total={progress.total} label={progress.command_name} />}
      <div className="space-y-2">
        {events.map((e, idx) => {
          if (e.type === "tool_call") return <ToolCallEntry key={idx} event={e} />;
          if (e.type === "skill_observation") return <ObservationCard key={idx} event={e} />;
          if (e.type === "done")
            return (
              <article key={idx} data-testid="done-card" className="rounded-md border border-emerald-300 bg-emerald-50 px-3 py-2 text-sm">
                <p className="font-medium">Done</p>
                <p className="text-xs">{e.summary}</p>
                {e.pr_url && (
                  <a className="text-xs underline" href={e.pr_url} target="_blank" rel="noreferrer">
                    {e.pr_url}
                  </a>
                )}
              </article>
            );
          if (e.type === "error")
            return (
              <article key={idx} data-testid="error-card" className="rounded-md border border-rose-300 bg-rose-50 px-3 py-2 text-sm">
                <p className="font-medium">Error</p>
                <p className="text-xs">{e.error_context}</p>
              </article>
            );
          return null;
        })}
      </div>
      {status === "closed" && events.length === 0 && (
        <p className="text-xs text-stone-500">Stream closed. No events received.</p>
      )}
    </section>
  );
}
