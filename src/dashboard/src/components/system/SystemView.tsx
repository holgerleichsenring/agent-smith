"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import { useSubsystemEvents } from "@/hooks/useSubsystemEvents";
import {
  useSubsystemActivity,
  SUBSYSTEMS,
  type SubsystemActivity,
  type SubsystemId,
} from "@/hooks/useSubsystemActivity";
import { ConnectionState } from "@/components/jobs/ConnectionState";
import { SubsystemDetail } from "@/components/system/SubsystemDetail";
import { CatalogBrowser } from "@/components/system/CatalogBrowser";
import { ConfigView } from "@/components/system/ConfigView";
import { ConnectionsView } from "@/components/system/ConnectionsView";
import { ExpectationMetricsView } from "@/components/system/ExpectationMetricsView";
import { RollupCardsView, type RollupView } from "@/components/system/RollupCards";
import { PageHead } from "@/components/system/PageHead";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";
import type { SystemActivitySnapshot } from "@/types/hub-events";
import type { HubConnectionState } from "@microsoft/signalr";

// p0209b: the System master/detail body. The selected subsystem comes from the
// route slug (resolved by app/system/[[...slug]]/page.tsx), so selection is
// URL-stable and survives refresh / deep-link by construction — no query param,
// no client selection state.
//   segment null                            → default subsystem (tracker)
//   tracker|webhooks|chat                    → SubsystemPage (KPI strip + stream)
//   config                                   → ConfigView (resolved-config sheet, then the read-events stream) (p0266)
//   catalog                                  → CatalogBrowser
//   cost|today                              → RollupCards metric strip (p0209c)
//   expectations                             → ExpectationMetricsView (p0329)
// p0343d: every route renders as a first-class page in the parity vocabulary —
// the .mock-shell/.mock-system scope, an .m-head title row, a .health KPI strip
// where the page has real numbers, section rules + rows/cards below. Data,
// hooks and behaviors are unchanged; this is the re-dress layer only.
// Lives in components/ (not the page file) so the page exports only its default,
// satisfying Next's Page-type contract while staying unit-testable on the slug.

const SUBSYSTEM_IDS = SUBSYSTEMS.map((s) => s.id) as SubsystemId[];
const ROLLUP_IDS = ["cost", "today"] as const;
const DEFAULT_SUBSYSTEM: SubsystemId = "tracker";

// Page voice for the three event-stream subsystems (config/catalog carry their
// own heads below; connections and the rollups render theirs in-view).
const STREAM_META: Record<"tracker" | "webhooks" | "chat", { title: string; sub: string }> = {
  tracker: {
    title: "Tracker",
    sub: "Ticket polling — every cycle the watch loop scans, matches, and spawns.",
  },
  webhooks: {
    title: "Webhooks",
    sub: "Inbound deliveries from your platforms — actioned or skipped, with the reason.",
  },
  chat: {
    title: "Chat dispatchers",
    sub: "Messages from connected chat channels — actioned or skipped, with the reason.",
  },
};

export function SystemView({ segment }: { segment: string | null }) {
  const { connectionState, systemActivity } = useJobsHub();

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
    <div className="mock-shell mock-system" data-testid="system-page">
      <main className="main">
        {isConnections ? (
          // p0292: the connections subsystem is an ACTIVE diagnostics surface — it
          // probes each configured repo/tracker on demand, not an event stream.
          <ConnectionsView />
        ) : isExpectations ? (
          <>
            <PageHead
              title="Expectations"
              sub="How often the drafted expectation hit the mark: hit rate is the share of drafts humans ratified verbatim; first-PR acceptance is the share of runs whose PR was built against a human-accepted contract."
            />
            <ExpectationMetricsView />
          </>
        ) : isRollup ? (
          <RollupCardsView view={segment as RollupView} />
        ) : subsystem === "catalog" ? (
          // p0221: the catalog subsystem is a system reference — it renders the
          // catalog's actual contents, not just its load-event stream.
          <>
            <PageHead
              title="Skill catalog & vocabulary"
              sub="The resolved catalog the masters actually load — masters, skills, and the concept vocabulary."
            />
            <CatalogBrowser />
          </>
        ) : subsystem === "config" ? (
          // p0266: the config subsystem renders the resolved-config sheet (the
          // config-time "how it's wired" view) above its read-events stream.
          <>
            <PageHead
              title="Config file reads"
              sub="How agent-smith is wired, and every config file the runtime actually read. Secrets are never sent to the dashboard."
              right={<ConnectionState state={connectionState} />}
            />
            <ConfigView activity={activity[subsystem]} />
          </>
        ) : (
          <SubsystemPage
            id={subsystem as "tracker" | "webhooks" | "chat"}
            activity={activity[subsystem]}
            snapshot={systemActivity}
            connectionState={connectionState}
          />
        )}
      </main>
    </div>
  );
}

// One event-stream page: .m-head, the page's REAL 24h KPIs off the server-truth
// SystemActivitySnapshot (chat has no server counters, so it honestly has no
// strip), then the typed event stream.
function SubsystemPage({
  id,
  activity,
  snapshot,
  connectionState,
}: {
  id: "tracker" | "webhooks" | "chat";
  activity: SubsystemActivity;
  snapshot: SystemActivitySnapshot | null;
  connectionState: HubConnectionState;
}) {
  const meta = STREAM_META[id];
  const cells = streamKpis(id, snapshot);
  return (
    <>
      <PageHead title={meta.title} sub={meta.sub} right={<ConnectionState state={connectionState} />} />
      {cells && <SystemMetricStrip testId={`system-kpis-${id}`} cells={cells} />}
      <SubsystemDetail activity={activity} heading="Event stream" />
    </>
  );
}

// "—" (not a fake 0) until the first server snapshot arrives.
function streamKpis(
  id: "tracker" | "webhooks" | "chat",
  snapshot: SystemActivitySnapshot | null,
): MetricCell[] | null {
  const v = (n: number) => (snapshot ? n : "—");
  switch (id) {
    case "tracker":
      return [
        { label: "Tickets scanned", value: v(snapshot?.ticketsScanned ?? 0), testId: "sys-metric-tickets-scanned" },
        { label: "Tickets triggered", value: v(snapshot?.ticketsTriggered ?? 0), testId: "sys-metric-tickets-triggered" },
        { label: "Tickets skipped", value: v(snapshot?.ticketsSkipped ?? 0), testId: "sys-metric-tickets-skipped" },
        { label: "Poll cycles", value: v(snapshot?.pollCyclesFinished ?? 0), small: "24h", testId: "sys-metric-poll-cycles" },
      ];
    case "webhooks":
      return [
        { label: "Received", value: v(snapshot?.webhooksReceived ?? 0), small: "24h", testId: "sys-metric-webhooks-received" },
        { label: "Actioned", value: v(snapshot?.webhooksActioned ?? 0), small: "24h", testId: "sys-metric-webhooks-actioned" },
      ];
    case "chat":
      // No server-truth chat counters on the snapshot — no strip is honest.
      return null;
  }
}
