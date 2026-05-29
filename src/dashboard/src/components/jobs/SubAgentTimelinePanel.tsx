"use client";

import { useState, useMemo } from "react";
import { EventType, type RunEvent } from "@/types/hub-events";
import { useSubAgentObservations, type SubAgentEventKind } from "@/hooks/useSubAgentObservations";
import { SubAgentObservationRow } from "./rows/SubAgentObservationRow";
import { SubAgentFindingRow } from "./rows/SubAgentFindingRow";
import { SubAgentFileWrittenRow } from "./rows/SubAgentFileWrittenRow";
import { SubAgentToolCallRow } from "./rows/SubAgentToolCallRow";

interface Props {
  runId: string;
  subAgentId: string;
}

const KIND_OPTIONS: { value: SubAgentEventKind; label: string }[] = [
  { value: "observation", label: "Observations" },
  { value: "finding", label: "Findings" },
  { value: "file_written", label: "Files" },
  { value: "tool_call", label: "Tools" },
];

export function SubAgentTimelinePanel({ runId, subAgentId }: Props) {
  const [activeKinds, setActiveKinds] = useState<Set<SubAgentEventKind>>(
    new Set<SubAgentEventKind>(),
  );

  const events = useSubAgentObservations(
    runId,
    subAgentId,
    activeKinds.size === 0 ? undefined : activeKinds,
  );

  const sorted = useMemo(
    () => [...events].sort((a, b) => Date.parse(a.timestamp) - Date.parse(b.timestamp)),
    [events],
  );

  return (
    <section className="space-y-3 text-xs" data-testid="sub-agent-timeline-panel">
      <header className="flex flex-wrap items-center gap-2">
        <h3 className="font-medium text-stone-700">
          Sub-agent <span className="font-mono">{subAgentId}</span>
        </h3>
        <div className="flex gap-1">
          {KIND_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              type="button"
              onClick={() => toggleKind(setActiveKinds, opt.value)}
              data-testid={`sub-agent-kind-chip-${opt.value}`}
              aria-pressed={activeKinds.has(opt.value)}
              className={`rounded-full border px-2 py-0.5 ${
                activeKinds.has(opt.value)
                  ? "border-emerald-500 bg-emerald-100 text-emerald-900"
                  : "border-stone-300 bg-white text-stone-700"
              }`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </header>
      {sorted.length === 0 ? (
        <p className="text-stone-500" data-testid="sub-agent-timeline-empty">
          (no observations yet)
        </p>
      ) : (
        <ul className="space-y-0">
          {sorted.map((event, idx) => (
            <li key={`${event.timestamp}-${idx}`}>{renderRow(event)}</li>
          ))}
        </ul>
      )}
    </section>
  );
}

function toggleKind(
  setter: React.Dispatch<React.SetStateAction<Set<SubAgentEventKind>>>,
  kind: SubAgentEventKind,
) {
  setter((prev) => {
    const next = new Set(prev);
    if (next.has(kind)) next.delete(kind);
    else next.add(kind);
    return next;
  });
}

function renderRow(event: RunEvent): React.ReactNode {
  switch (event.type) {
    case EventType.SubAgentObservation:
      return <SubAgentObservationRow event={event} />;
    case EventType.SubAgentFinding:
      return <SubAgentFindingRow event={event} />;
    case EventType.SubAgentFileWritten:
      return <SubAgentFileWrittenRow event={event} />;
    case EventType.SubAgentToolCall:
      return <SubAgentToolCallRow event={event} />;
    default:
      return null;
  }
}
