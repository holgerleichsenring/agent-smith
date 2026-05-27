"use client";

import { useCallback, useMemo, useState } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useRunEvents } from "@/hooks/useRunEvents";
import {
  ALL_PILLS,
  isEventVisible,
  parsePillsFromQuery,
  writePillsToQuery,
  type ActivityPill,
} from "@/lib/activityPillQuery";
import { ActivityPills } from "./ActivityPills";
import { ActivityRow } from "./ActivityRow";
import { FilterRail } from "./FilterRail";

interface Props {
  runId: string;
}

export function ActivityTab({ runId }: Props) {
  const events = useRunEvents(runId);
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const pills = useMemo(
    () => parsePillsFromQuery(new URLSearchParams(searchParams.toString())),
    [searchParams],
  );

  const updatePills = useCallback(
    (next: ReadonlySet<ActivityPill>) => {
      const params = writePillsToQuery(next, new URLSearchParams(searchParams.toString()));
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname);
    },
    [pathname, router, searchParams],
  );

  const toggle = useCallback(
    (pill: ActivityPill) => {
      const next = new Set(pills);
      if (next.has(pill)) next.delete(pill);
      else next.add(pill);
      updatePills(next);
    },
    [pills, updatePills],
  );

  const onAll = useCallback(() => updatePills(new Set(ALL_PILLS)), [updatePills]);
  const onNone = useCallback(() => updatePills(new Set()), [updatePills]);

  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const toggleRow = useCallback((idx: number) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(idx)) next.delete(idx);
      else next.add(idx);
      return next;
    });
  }, []);

  const visible = useMemo(
    () => events.filter((e) => isEventVisible(e.type, pills)),
    [events, pills],
  );

  const [showDebugFilters, setShowDebugFilters] = useState(false);

  return (
    <div className="space-y-4" data-testid="activity-tab">
      <ActivityPills pills={pills} onToggle={toggle} onAll={onAll} onNone={onNone} />
      <div>
        <button
          type="button"
          onClick={() => setShowDebugFilters((v) => !v)}
          className="text-xs text-stone-500 hover:text-stone-700 hover:underline"
          data-testid="activity-debug-toggle"
          aria-expanded={showDebugFilters}
        >
          {showDebugFilters ? "Hide event types" : "Show event types"}
        </button>
        {showDebugFilters ? (
          <div className="mt-2 rounded border border-dashed border-stone-300 p-3" data-testid="activity-debug-rail">
            <FilterRail />
          </div>
        ) : null}
      </div>
      {visible.length === 0 ? (
        <p className="text-sm text-stone-500" data-testid="activity-empty">
          No matching activity yet.
        </p>
      ) : (
        <div className="space-y-1">
          {visible.map((event, idx) => (
            <ActivityRow
              key={`${event.timestamp}-${event.type}-${idx}`}
              event={event}
              expanded={expanded.has(idx)}
              onToggle={() => toggleRow(idx)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
