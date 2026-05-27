"use client";

import type { SystemActivitySnapshot } from "@/types/hub-events";

interface Props {
  activity: SystemActivitySnapshot | null;
}

interface Row {
  source: string;
  count: number;
}

// p0175-fix: reads per-source counts from the server-computed 24h rollup.
// Old client-derived useChannelBreakdown path counted every event in the
// local ring buffer (no time window, capped at 500 events).

export function ChannelBreakdown({ activity }: Props) {
  const rows: Row[] = activity
    ? Object.entries(activity.eventsPerSource)
        .map(([source, count]) => ({ source, count }))
        .sort((a, b) => b.count - a.count || a.source.localeCompare(b.source))
    : [];
  const total = rows.reduce((sum, row) => sum + row.count, 0);

  return (
    <section
      className="rounded-lg border border-stone-200 bg-white p-4"
      data-testid="channel-breakdown"
    >
      <h2 className="mb-3 text-sm font-medium text-stone-700">By source</h2>
      {rows.length === 0 ? (
        <p className="text-sm text-stone-500" data-testid="channel-breakdown-empty">
          No system events yet.
        </p>
      ) : (
        <ul className="space-y-1.5 text-sm">
          {rows.map((row) => {
            const pct = total > 0 ? Math.round((row.count / total) * 100) : 0;
            return (
              <li
                key={row.source}
                className="flex items-center gap-3"
                data-testid={`channel-row-${row.source}`}
              >
                <span className="w-44 truncate font-mono text-xs text-stone-700">
                  {row.source}
                </span>
                <span className="relative h-1.5 flex-1 overflow-hidden rounded-full bg-stone-100">
                  <span
                    className="absolute inset-y-0 left-0 rounded-full bg-stone-500"
                    style={{ width: `${pct}%` }}
                  />
                </span>
                <span className="w-12 text-right tabular-nums text-xs text-stone-600">{row.count}</span>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
