"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useSystemEvents } from "@/hooks/useSystemEvents";
import { useSubsystemActivity, SUBSYSTEMS, type SubsystemId } from "@/hooks/useSubsystemActivity";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { SubsystemDetail } from "@/components/system/SubsystemDetail";
import { RollupCardsView, type RollupView } from "@/components/system/RollupCards";

// p0209b: the System master/detail body. The selected subsystem comes from the
// route slug (resolved by app/system/[[...slug]]/page.tsx), so selection is
// URL-stable and survives refresh / deep-link by construction — no query param,
// no client selection state.
//   segment null                            → default subsystem (tracker)
//   tracker|webhooks|chat|config|catalog    → SubsystemDetail
//   cost|today                              → RollupCards KPI grid (p0209c)
// Lives in components/ (not the page file) so the page exports only its default,
// satisfying Next's Page-type contract while staying unit-testable on the slug.

const SUBSYSTEM_IDS = SUBSYSTEMS.map((s) => s.id) as SubsystemId[];
const ROLLUP_IDS = ["cost", "today"] as const;
const DEFAULT_SUBSYSTEM: SubsystemId = "tracker";

export function SystemView({ segment }: { segment: string | null }) {
  const { connectionState } = useJobsHub();
  const events = useSystemEvents();
  const activity = useSubsystemActivity(events);

  const isRollup = segment != null && (ROLLUP_IDS as readonly string[]).includes(segment);
  const subsystem: SubsystemId = isRollup
    ? DEFAULT_SUBSYSTEM
    : segment != null && (SUBSYSTEM_IDS as string[]).includes(segment)
      ? (segment as SubsystemId)
      : DEFAULT_SUBSYSTEM;

  return (
    <div className="flex h-full flex-col">
      <header className="flex items-start justify-between gap-4 px-7 pt-6">
        <p className="text-sm text-stone-500">
          What the watch loop is doing right now — pick a subsystem from the rail
          to see its typed event stream with filter, sort and search.
        </p>
        <ConnectionState state={connectionState} />
      </header>

      {isRollup ? (
        <RollupCardsView view={segment as RollupView} />
      ) : (
        <SubsystemDetail activity={activity[subsystem]} />
      )}
    </div>
  );
}
