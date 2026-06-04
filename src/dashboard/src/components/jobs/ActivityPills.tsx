"use client";

import type { ActivityPill } from "@/lib/activityPillQuery";
import { ALL_PILLS } from "@/lib/activityPillQuery";
import { Chip } from "@/components/ui/Chip";

interface Props {
  pills: ReadonlySet<ActivityPill>;
  onToggle: (pill: ActivityPill) => void;
  onAll: () => void;
  onNone: () => void;
}

const LABELS: Record<ActivityPill, string> = {
  decisions: "Decisions",
  tools: "Tools",
  llm: "LLM",
  sandbox: "Sandbox",
  gates: "Gates",
  issues: "Issues",
};

export function ActivityPills({ pills, onToggle, onAll, onNone }: Props) {
  const allActive = pills.size === ALL_PILLS.length;
  return (
    <div
      className="flex flex-wrap items-center gap-2 text-xs"
      role="toolbar"
      aria-label="Activity filters"
      data-testid="activity-pills"
    >
      <Chip
        testId="activity-pill-all"
        label={allActive ? "None" : "All"}
        selected={allActive}
        ariaPressed={allActive}
        onClick={allActive ? onNone : onAll}
      />
      <span className="text-stone-300" aria-hidden>
        ·
      </span>
      {ALL_PILLS.map((pill) => (
        <Chip
          key={pill}
          testId={`activity-pill-${pill}`}
          label={LABELS[pill]}
          selected={pills.has(pill)}
          onClick={() => onToggle(pill)}
        />
      ))}
    </div>
  );
}
