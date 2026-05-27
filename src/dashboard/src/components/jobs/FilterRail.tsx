"use client";

import { useEventFilter } from "@/lib/EventFilterContext";
import { EventType } from "@/types/hub-events";
import { L1_TYPES, L2_TYPES, L3_TYPES } from "@/lib/eventFilterQuery";

const LABELS: Record<EventType, string> = {
  [EventType.RunStarted]: "RunStarted",
  [EventType.RunFinished]: "RunFinished",
  [EventType.SandboxCreated]: "SandboxCreated",
  [EventType.SandboxDisposed]: "SandboxDisposed",
  [EventType.StepStarted]: "StepStarted",
  [EventType.StepFinished]: "StepFinished",
  [EventType.DecisionLogged]: "DecisionLogged",
  [EventType.GateChecked]: "GateChecked",
  [EventType.TriageRoute]: "TriageRoute",
  [EventType.LlmCallStarted]: "LlmCallStarted",
  [EventType.LlmCallFinished]: "LlmCallFinished",
  [EventType.SandboxCommand]: "SandboxCommand",
  [EventType.SandboxOutput]: "SandboxOutput",
  [EventType.SandboxResult]: "SandboxResult",
  [EventType.ToolCall]: "ToolCall",
  [EventType.ToolResult]: "ToolResult",
  [EventType.CatalogIssue]: "CatalogIssue",
};

export function FilterRail() {
  const { state, toggle } = useEventFilter();
  return (
    <aside className="space-y-4 text-xs" data-testid="filter-rail">
      <Section
        title="L1 topology"
        level="l1"
        types={[...L1_TYPES]}
        active={state.l1}
        onToggle={(t) => toggle("l1", t)}
      />
      <Section
        title="L2 decisions"
        level="l2"
        types={[...L2_TYPES]}
        active={state.l2}
        onToggle={(t) => toggle("l2", t)}
      />
      <Section
        title="L3 sandbox"
        level="l3"
        types={[...L3_TYPES]}
        active={state.l3}
        onToggle={(t) => toggle("l3", t)}
      />
    </aside>
  );
}

interface SectionProps {
  title: string;
  level: "l1" | "l2" | "l3";
  types: EventType[];
  active: ReadonlySet<EventType>;
  onToggle: (type: EventType) => void;
}

function Section({ title, level, types, active, onToggle }: SectionProps) {
  return (
    <div>
      <h3 className="mb-2 font-medium text-stone-700">{title}</h3>
      <ul className="space-y-1" data-testid={`filter-section-${level}`}>
        {types.map((t) => (
          <li key={t} className="flex items-center gap-2">
            <input
              id={`filter-${level}-${t}`}
              type="checkbox"
              checked={active.has(t)}
              onChange={() => onToggle(t)}
              data-testid={`filter-toggle-${LABELS[t]}`}
            />
            <label htmlFor={`filter-${level}-${t}`} className="text-stone-700">
              {LABELS[t]}
            </label>
          </li>
        ))}
      </ul>
    </div>
  );
}
