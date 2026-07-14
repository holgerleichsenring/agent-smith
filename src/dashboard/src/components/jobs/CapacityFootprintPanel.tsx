"use client";

import type { RunFootprintView } from "@/types/hub-events";

// p0336: the run's capacity calculation, made visible — every pod it needs with
// its resolved limit, the total vs the reservation, the FIFO position while it
// waits, and what scoping dropped (+why). Shows the operator WHY N pods and
// whether it fits, before and while the run waits.
interface Props {
  footprint: RunFootprintView;
  queuePosition?: number | null;
}

export function CapacityFootprintPanel({ footprint, queuePosition }: Props) {
  const badge = footprint.reserved
    ? { label: "reserved", tone: "bg-emerald-100 text-emerald-700" }
    : queuePosition != null
      ? { label: `queued · #${queuePosition}`, tone: "bg-amber-100 text-amber-700" }
      : { label: "waiting", tone: "bg-amber-100 text-amber-700" };

  return (
    <div
      data-testid="capacity-footprint-panel"
      className="mt-3 rounded-lg border border-stone-200 bg-stone-50 px-4 py-3 text-sm"
    >
      <div className="flex items-center justify-between">
        <span className="font-semibold text-stone-700">Capacity footprint</span>
        <span className={`rounded px-1.5 py-0.5 text-xs font-medium ${badge.tone}`}>{badge.label}</span>
      </div>
      <div className="mt-1 font-mono text-xs text-stone-500">
        {footprint.pods.length} pods · {footprint.totalMemLimit} / {footprint.totalCpuLimit} cpu
      </div>

      <ul className="mt-2 space-y-1">
        {footprint.pods.map((pod, i) => (
          <li key={i} className="flex items-center justify-between gap-3 font-mono text-xs text-stone-600">
            <span className="truncate">
              {pod.repo}
              {pod.contexts.length > 0 ? ` / ${pod.contexts.join("+")}` : ""}{" "}
              <span className="text-stone-400">({pod.image})</span>
            </span>
            <span className="flex-none text-stone-500">
              {pod.memLimit} / {pod.cpuLimit}
            </span>
          </li>
        ))}
      </ul>

      {footprint.dropped.length > 0 && (
        <div className="mt-2 border-t border-stone-200 pt-2">
          <span className="text-xs font-medium text-stone-500">Dropped by scoping</span>
          <ul className="mt-1 space-y-0.5">
            {footprint.dropped.map((d, i) => (
              <li key={i} className="font-mono text-xs text-stone-400">
                − {d.repo}
                {d.context ? `/${d.context}` : ""} — {d.reason}
              </li>
            ))}
          </ul>
        </div>
      )}

      {footprint.reason && <div className="mt-2 text-xs text-stone-400">{footprint.reason}</div>}
    </div>
  );
}
