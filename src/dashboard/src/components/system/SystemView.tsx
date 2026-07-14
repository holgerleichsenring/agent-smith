"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useSubsystemEvents } from "@/hooks/useSubsystemEvents";
import { useSubsystemActivity, SUBSYSTEMS, type SubsystemId } from "@/hooks/useSubsystemActivity";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { SubsystemDetail } from "@/components/system/SubsystemDetail";
import { CatalogBrowser } from "@/components/system/CatalogBrowser";
import { ConfigView } from "@/components/system/ConfigView";
import { ConnectionsView } from "@/components/system/ConnectionsView";
import { ExpectationMetricsView } from "@/components/system/ExpectationMetricsView";
import { RollupCardsView, type RollupView } from "@/components/system/RollupCards";

// p0209b: the System master/detail body. The selected subsystem comes from the
// route slug (resolved by app/system/[[...slug]]/page.tsx), so selection is
// URL-stable and survives refresh / deep-link by construction — no query param,
// no client selection state.
//   segment null                            → default subsystem (tracker)
//   tracker|webhooks|chat                    → SubsystemDetail
//   config                                   → ConfigView (resolved-config graph + detail, then the read-events stream) (p0266)
//   catalog                                  → CatalogBrowser
//   cost|today                              → RollupCards KPI grid (p0209c)
//   expectations                             → ExpectationMetricsView (p0329)
// Lives in components/ (not the page file) so the page exports only its default,
// satisfying Next's Page-type contract while staying unit-testable on the slug.

const SUBSYSTEM_IDS = SUBSYSTEMS.map((s) => s.id) as SubsystemId[];
const ROLLUP_IDS = ["cost", "today"] as const;
const DEFAULT_SUBSYSTEM: SubsystemId = "tracker";

export function SystemView({ segment }: { segment: string | null }) {
  const { connectionState } = useJobsHub();

  const isConnections = segment === "connections";
  // p0329: expectation metrics — a REST-fed rollup like connections, not an
  // event-stream subsystem.
  const isExpectations = segment === "expectations";
  const isRollup = segment != null && (ROLLUP_IDS as readonly string[]).includes(segment);
  const subsystem: SubsystemId = isRollup
    ? DEFAULT_SUBSYSTEM
    : segment != null && (SUBSYSTEM_IDS as string[]).includes(segment)
      ? (segment as SubsystemId)
      : DEFAULT_SUBSYSTEM;

  // Read only the selected subsystem's scope from the shared store — the
  // detail pane no longer subscribes to the whole system firehose.
  const events = useSubsystemEvents(subsystem);
  const activity = useSubsystemActivity(events);

  return (
    <div className="flex h-full flex-col">
      <header className="flex items-start justify-between gap-4 px-6 pt-6">
        <p className="text-sm text-stone-500">
          What the watch loop is doing right now — pick a subsystem from the rail
          to see its typed event stream with filter, sort and search.
        </p>
        <ConnectionState state={connectionState} />
      </header>

      {isConnections ? (
        // p0292: the connections subsystem is an ACTIVE diagnostics surface — it
        // probes each configured repo/tracker on demand, not an event stream.
        <ConnectionsView />
      ) : isExpectations ? (
        <ExpectationMetricsView />
      ) : isRollup ? (
        <RollupCardsView view={segment as RollupView} />
      ) : subsystem === "catalog" ? (
        // p0221: the catalog subsystem is a system reference — it renders the
        // catalog's actual contents, not just its load-event stream.
        <CatalogBrowser />
      ) : subsystem === "config" ? (
        // p0266: the config subsystem renders the resolved-config graph + detail
        // (the config-time "how it's wired" view) above its read-events stream.
        <ConfigView activity={activity[subsystem]} />
      ) : (
        <SubsystemDetail activity={activity[subsystem]} />
      )}
    </div>
  );
}
