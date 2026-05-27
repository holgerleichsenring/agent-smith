"use client";

import { useSystemStatus } from "@/hooks/useSystemStatus";
import type { SystemEvent } from "@/types/system-events";
import { StatusPill } from "./StatusPill";

interface Props {
  events: readonly SystemEvent[];
}

export function SystemStatusCard({ events }: Props) {
  const status = useSystemStatus(events);

  return (
    <section
      className="rounded-lg border border-stone-200 bg-white p-4"
      data-testid="system-status-card"
    >
      <h2 className="mb-3 text-sm font-medium text-stone-700">Providers</h2>
      {status.length === 0 ? (
        <p className="text-sm text-stone-500" data-testid="system-status-empty">
          No poll cycles observed yet. Waiting for the polling loop.
        </p>
      ) : (
        <ul className="space-y-2">
          {status.map((row) => (
            <li
              key={row.source}
              className="flex items-center justify-between gap-3 text-sm"
              data-testid={`system-status-row-${row.source}`}
            >
              <span className="flex items-center gap-2">
                <StatusPill status={row.status} />
                <span className="text-stone-800">{row.tracker}</span>
                <span className="font-mono text-xs text-stone-500">{row.source}</span>
              </span>
              <span className="text-xs text-stone-500">
                next poll {row.nextEtaAt !== null ? new Date(row.nextEtaAt).toLocaleTimeString() : "—"}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
