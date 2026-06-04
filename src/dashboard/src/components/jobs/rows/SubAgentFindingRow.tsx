"use client";

import type { SubAgentFindingEvent } from "@/types/hub-events";

// p0173f: typed renderer for SubAgentFindingEvent. Severity badge,
// title, detail body — all from typed fields. The dashboard never
// shows the raw event payload here.
export function SubAgentFindingRow({ event }: { event: SubAgentFindingEvent }) {
  return (
    <div
      data-testid="sub-agent-finding-row"
      className="flex items-start gap-2 border-b border-stone-100 py-1 text-xs"
    >
      <span
        data-testid="sub-agent-finding-severity"
        className={`shrink-0 rounded px-1.5 py-0.5 dsh-label font-semibold uppercase ${severityClass(event.severity)}`}
      >
        {event.severity}
      </span>
      <div className="flex-1">
        <p data-testid="sub-agent-finding-title" className="font-medium text-stone-800">
          {event.title}
        </p>
        <p data-testid="sub-agent-finding-detail" className="text-stone-700">
          {event.detail}
        </p>
      </div>
    </div>
  );
}

function severityClass(severity: string): string {
  switch (severity.toLowerCase()) {
    case "critical": return "bg-rose-200 text-rose-900";
    case "high": return "bg-amber-200 text-amber-900";
    case "medium": return "bg-amber-100 text-amber-900";
    case "low": return "bg-stone-200 text-stone-800";
    default: return "bg-stone-100 text-stone-700";
  }
}
