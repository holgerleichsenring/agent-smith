"use client";

import { useEventFilter } from "@/lib/EventFilterContext";
import { EventType } from "@/types/hub-events";
import { L1_TYPES, L2_TYPES, L3_TYPES } from "@/lib/eventFilterQuery";
import type { DimensionKey } from "@/lib/dimensionFilterQuery";

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
  [EventType.L1StepDetail]: "L1StepDetail",
  [EventType.TicketFetched]: "TicketFetched",
  [EventType.CatalogLoaded]: "CatalogLoaded",
  [EventType.PullRequestOutcome]: "PullRequestOutcome",
  [EventType.CatalogIssue]: "CatalogIssue",
  [EventType.TicketInstructionIgnored]: "TicketInstructionIgnored",
  [EventType.SubAgentSpawned]: "SubAgentSpawned",
  [EventType.SubAgentObservation]: "SubAgentObservation",
  [EventType.SubAgentFinding]: "SubAgentFinding",
  [EventType.SubAgentFileWritten]: "SubAgentFileWritten",
  [EventType.SubAgentToolCall]: "SubAgentToolCall",
  [EventType.SubAgentCompleted]: "SubAgentCompleted",
  [EventType.RunCancelRequested]: "RunCancelRequested",
  [EventType.SandboxVanished]: "SandboxVanished",
  [EventType.RunCheckpointed]: "RunCheckpointed", // p0327
  [EventType.ExpectationRatified]: "ExpectationRatified", // p0328
};

interface FilterRailProps {
  /** p0173f: observed dimension values per group; usually derived from the run-event stream. */
  observedDimensions?: {
    agent?: string[];
    sandbox?: string[];
    pipeline?: string[];
    activity?: string[];
  };
}

export function FilterRail({ observedDimensions }: FilterRailProps = {}) {
  const { state, toggle, dimensions, toggleDimension } = useEventFilter();
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
      <DimensionSection
        title="Agent"
        dimensionKey="agent"
        values={observedDimensions?.agent ?? []}
        active={dimensions.agent}
        onToggle={(v) => toggleDimension("agent", v)}
      />
      <DimensionSection
        title="Sandbox"
        dimensionKey="sandbox"
        values={observedDimensions?.sandbox ?? []}
        active={dimensions.sandbox}
        onToggle={(v) => toggleDimension("sandbox", v)}
      />
      <DimensionSection
        title="Pipeline"
        dimensionKey="pipeline"
        values={observedDimensions?.pipeline ?? []}
        active={dimensions.pipeline}
        onToggle={(v) => toggleDimension("pipeline", v)}
      />
      <DimensionSection
        title="Activity"
        dimensionKey="activity"
        values={observedDimensions?.activity ?? []}
        active={dimensions.activity}
        onToggle={(v) => toggleDimension("activity", v)}
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

interface DimensionSectionProps {
  title: string;
  dimensionKey: DimensionKey;
  values: string[];
  active: ReadonlySet<string>;
  onToggle: (value: string) => void;
}

function DimensionSection({ title, dimensionKey, values, active, onToggle }: DimensionSectionProps) {
  return (
    <div data-testid={`filter-dim-${dimensionKey}`}>
      <h3 className="mb-2 font-medium text-stone-700">{title}</h3>
      {values.length === 0 ? (
        <p className="text-stone-500" data-testid={`filter-dim-${dimensionKey}-empty`}>
          (no values observed yet)
        </p>
      ) : (
        <ul className="flex flex-wrap gap-1">
          {values.map((v) => (
            <li key={v}>
              <button
                type="button"
                onClick={() => onToggle(v)}
                data-testid={`filter-chip-${dimensionKey}-${v}`}
                aria-pressed={active.has(v)}
                className={`rounded-full border px-2 py-0.5 text-xs ${
                  active.has(v)
                    ? "border-emerald-500 bg-emerald-100 text-emerald-900"
                    : "border-stone-300 bg-white text-stone-700"
                }`}
              >
                {v}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
