"use client";

import { skipReasonLabel, useTriggerLog } from "@/hooks/useTriggerLog";
import type { SystemEvent } from "@/types/system-events";

interface Props {
  events: readonly SystemEvent[];
  limit?: number;
}

export function TriggerLog({ events, limit = 50 }: Props) {
  const rows = useTriggerLog(events, limit);

  return (
    <section
      className="rounded-lg border border-stone-200 bg-white p-4"
      data-testid="trigger-log"
    >
      <h2 className="mb-3 text-sm font-medium text-stone-700">Recent trigger decisions</h2>
      {rows.length === 0 ? (
        <p className="text-sm text-stone-500" data-testid="trigger-log-empty">
          No trigger decisions yet.
        </p>
      ) : (
        <ul className="space-y-1.5 text-sm">
          {rows.map((row, idx) => (
            <li
              key={`${row.timestamp}-${row.ticketId}-${idx}`}
              className={`flex flex-col gap-0.5 rounded border px-3 py-2 ${
                row.kind === "triggered"
                  ? "border-emerald-200 bg-emerald-50"
                  : "border-amber-200 bg-amber-50"
              }`}
              data-testid={`trigger-row-${row.kind}`}
            >
              <span className="flex items-center justify-between gap-3 text-xs">
                <span className="flex items-center gap-2">
                  <span className="font-mono text-stone-500">
                    {new Date(row.timestamp).toLocaleTimeString()}
                  </span>
                  <span className="font-medium text-stone-800">
                    {row.tracker}/{row.ticketId}
                  </span>
                </span>
                <span className="text-stone-500">{row.source}</span>
              </span>
              {row.kind === "triggered" ? (
                <span className="text-xs text-emerald-800">
                  → {row.project}/{row.pipeline} ({row.outcome})
                </span>
              ) : (
                <span className="text-xs text-amber-800">
                  skipped — {skipReasonLabel(row.reason)}: {row.detail}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
