"use client";

import type { ActivityPill } from "@/lib/activityPillQuery";
import { ALL_PILLS } from "@/lib/activityPillQuery";
import { Chip } from "@/components/ui/Chip";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

interface Props {
  pills: ReadonlySet<ActivityPill>;
  onToggle: (pill: ActivityPill) => void;
  onAll: () => void;
  onNone: () => void;
}

const LABELS: Record<ActivityPill, TextKey> = {
  decisions: "runs.activity.pill.decisions",
  tools: "runs.activity.pill.tools",
  llm: "runs.activity.pill.llm",
  sandbox: "runs.activity.pill.sandbox",
  gates: "runs.activity.pill.gates",
  issues: "runs.activity.pill.issues",
};

export function ActivityPills({ pills, onToggle, onAll, onNone }: Props) {
  const t = useT();
  const allActive = pills.size === ALL_PILLS.length;
  return (
    <div
      className="flex flex-wrap items-center gap-2 text-xs"
      role="toolbar"
      aria-label={t("runs.activity.filters")}
      data-testid="activity-pills"
    >
      <Chip
        testId="activity-pill-all"
        label={allActive ? t("runs.activity.none") : t("runs.mission.all")}
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
          label={t(LABELS[pill])}
          selected={pills.has(pill)}
          onClick={() => onToggle(pill)}
        />
      ))}
    </div>
  );
}
