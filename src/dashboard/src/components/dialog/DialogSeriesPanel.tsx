"use client";

import type { SeriesRunView } from "@/types/series";

// 2026-11-17-0207e: the series a filing runs as, in the right column — progress, spend and the estimated finish,
// the critical path and the policy. Plain text, the ticket page's sections as words. 2026-11-19-1b72a: every ticket
// as a hairline row — key, title, state, cost, time — in place of the Now list, since the conversation no longer
// carries the table.

export function DialogSeriesPanel({ series }: { series: SeriesRunView }) {
  const width = series.total === 0 ? 0 : Math.round((series.landed / series.total) * 100);
  return (
    <div data-testid="dialog-series" className="flex flex-col gap-4">
      <section className="flex flex-col gap-1">
        <p className="dsh-label text-mute">Series</p>
        <p className="dsh-body text-ink" data-testid="dialog-series-progress">
          {series.landed} of {series.total} landed · {series.elapsed}
        </p>
        <div className="d-series-bar" aria-hidden="true">
          <div className="d-series-fill" style={{ width: `${width}%` }} />
        </div>
        <p className="dsh-body text-mute" data-testid="dialog-series-cost">
          {series.cost} spent{series.eta ? ` · est. finish ${series.eta}` : ""}
        </p>
      </section>
      {series.tickets.length > 0 && (
        <section className="flex flex-col gap-1">
          <p className="dsh-label text-mute">Tickets</p>
          <ul className="dl-series-rows" data-testid="dialog-series-tickets">
            {series.tickets.map((row) => (
              <li key={row.ticket} data-kind={row.kind} className="dl-series-row dsh-body">
                <span className="font-mono text-ink">{row.ticket}</span>
                <span className="dl-series-what text-ink" title={row.what}>{row.what}</span>
                <span className="dl-series-ticket-state">{row.state}</span>
                <span className="dl-series-num text-mute">{row.cost}</span>
                <span className="dl-series-num text-mute">{row.time}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
      {series.criticalPath.length > 0 && (
        <section className="flex flex-col gap-1">
          <p className="dsh-label text-mute">Critical path</p>
          <p className="dsh-body font-mono text-ink" data-testid="dialog-series-path">{series.criticalPath.join(" → ")}</p>
          {series.criticalPathLeft && <p className="dsh-body text-mute">{series.criticalPathLeft}</p>}
        </section>
      )}
      <section className="flex flex-col gap-1">
        <p className="dsh-label text-mute">Policy</p>
        <p className="dsh-body text-mute" data-testid="dialog-series-policy">{series.policy}</p>
      </section>
    </div>
  );
}
