"use client";

// p0173f: render one sub-agent in the trail tree — Name badge, Activity
// one-liner, four decision-anchor count chips, status indicator. Counts
// come from SubAgentCompletedEvent (terminal) or accrue from the
// observation stream while the run is in flight.

interface SubAgentTreeNodeProps {
  subAgentId: string;
  name: string;
  activity: string;
  observationsCount: number;
  findingsCount: number;
  filesWrittenCount: number;
  toolCalls: number;
  status?: "Running" | "Succeeded" | "Failed";
  onSelect?: () => void;
  isSelected?: boolean;
}

export function SubAgentTreeNode({
  subAgentId,
  name,
  activity,
  observationsCount,
  findingsCount,
  filesWrittenCount,
  toolCalls,
  status = "Running",
  onSelect,
  isSelected = false,
}: SubAgentTreeNodeProps) {
  return (
    <button
      type="button"
      onClick={onSelect}
      data-testid={`sub-agent-node-${subAgentId}`}
      aria-pressed={isSelected}
      className={`flex w-full items-center gap-2 rounded border px-2 py-1 text-left text-xs ${
        isSelected
          ? "border-emerald-500 bg-emerald-50"
          : "border-stone-300 bg-white"
      }`}
    >
      <span
        className="rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-label font-semibold text-stone-800"
        data-testid={`sub-agent-name-${subAgentId}`}
      >
        {name}
      </span>
      <span
        className="flex-1 truncate text-stone-700"
        data-testid={`sub-agent-activity-${subAgentId}`}
      >
        {activity}
      </span>
      <CountChip label="obs" value={observationsCount} testId={`chip-obs-${subAgentId}`} />
      <CountChip label="find" value={findingsCount} testId={`chip-findings-${subAgentId}`} />
      <CountChip label="files" value={filesWrittenCount} testId={`chip-files-${subAgentId}`} />
      <CountChip label="tools" value={toolCalls} testId={`chip-tools-${subAgentId}`} />
      <StatusIndicator status={status} testId={`status-${subAgentId}`} />
    </button>
  );
}

interface CountChipProps {
  label: string;
  value: number;
  testId: string;
}

function CountChip({ label, value, testId }: CountChipProps) {
  return (
    <span
      data-testid={testId}
      className="rounded bg-stone-100 px-1.5 py-0.5 dsh-label text-stone-700"
    >
      {value} {label}
    </span>
  );
}

interface StatusIndicatorProps {
  status: "Running" | "Succeeded" | "Failed";
  testId: string;
}

function StatusIndicator({ status, testId }: StatusIndicatorProps) {
  const className = status === "Succeeded"
    ? "bg-emerald-500"
    : status === "Failed"
      ? "bg-rose-500"
      : "bg-stone-400 animate-pulse";
  return (
    <span
      data-testid={testId}
      className={`inline-block h-2 w-2 rounded-full ${className}`}
      aria-label={status}
    />
  );
}
