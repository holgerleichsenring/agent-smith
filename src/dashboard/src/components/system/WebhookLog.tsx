"use client";

import { useWebhookLog } from "@/hooks/useWebhookLog";
import type { SystemEvent } from "@/types/system-events";

interface Props {
  events: readonly SystemEvent[];
  limit?: number;
}

export function WebhookLog({ events, limit = 50 }: Props) {
  const entries = useWebhookLog(events, limit);

  return (
    <section
      className="rounded-md border border-stone-200 bg-white p-4"
      data-testid="webhook-log"
    >
      <header className="mb-3 flex items-baseline justify-between">
        <h2 className="text-sm font-medium text-stone-700">Webhook deliveries</h2>
        <span className="text-xs text-stone-500">{entries.length} most recent</span>
      </header>
      {entries.length === 0 ? (
        <p className="text-sm text-stone-500" data-testid="webhook-log-empty">
          No webhook deliveries yet.
        </p>
      ) : (
        <ul className="space-y-1.5">
          {entries.map((entry, idx) => (
            <li
              key={`${entry.timestamp}-${idx}`}
              className={`flex items-center justify-between gap-3 rounded border px-3 py-2 text-sm ${
                entry.actioned
                  ? "border-emerald-200 bg-emerald-50"
                  : "border-amber-200 bg-amber-50"
              }`}
              data-testid={`webhook-row-${entry.actioned ? "actioned" : "skipped"}`}
            >
              <span className="flex items-center gap-2">
                <span className="rounded bg-stone-800 px-1.5 py-0.5 font-mono text-[10px] uppercase tracking-wide text-stone-50">
                  webhook
                </span>
                <span className="font-medium text-stone-800">{entry.source.replace(/^webhook:/, "")}</span>
                <span className="font-mono text-xs text-stone-500">{entry.eventType}</span>
                <span className="font-mono text-xs text-stone-400">{entry.path}</span>
              </span>
              <span className="flex items-center gap-3 text-xs">
                <span className="font-mono text-stone-500">
                  {new Date(entry.timestamp).toLocaleTimeString()}
                </span>
                {entry.actioned ? (
                  <span className="rounded bg-emerald-100 px-1.5 py-0.5 text-emerald-700">actioned</span>
                ) : (
                  <span className="rounded bg-amber-100 px-1.5 py-0.5 text-amber-700">
                    {entry.skipReason ?? "skipped"}
                  </span>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
