"use client";

import type { ActivityPill } from "@/lib/activityPillQuery";
import { ALL_PILLS } from "@/lib/activityPillQuery";

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
      <button
        type="button"
        onClick={allActive ? onNone : onAll}
        className={pillClass(allActive)}
        data-testid="activity-pill-all"
        aria-pressed={allActive}
      >
        {allActive ? "None" : "All"}
      </button>
      <span className="text-stone-300" aria-hidden>
        ·
      </span>
      {ALL_PILLS.map((pill) => {
        const active = pills.has(pill);
        return (
          <button
            type="button"
            key={pill}
            onClick={() => onToggle(pill)}
            className={pillClass(active)}
            data-testid={`activity-pill-${pill}`}
            aria-pressed={active}
          >
            {LABELS[pill]}
          </button>
        );
      })}
    </div>
  );
}

function pillClass(active: boolean): string {
  const base =
    "rounded-full border px-3 py-1 transition-colors focus:outline-none focus:ring-2 focus:ring-offset-1 focus:ring-stone-400";
  if (active) {
    return `${base} border-stone-800 bg-stone-800 text-stone-50`;
  }
  return `${base} border-stone-300 bg-white text-stone-600 hover:bg-stone-100`;
}
