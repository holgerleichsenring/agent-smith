"use client";

import { useJobsHub } from "@/hooks/useJobsHub";
import type { ApiRefusal } from "@/lib/apiResponse";
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
import { PageHead } from "@/components/system/PageHead";
import { SystemMetricStrip, type MetricCell } from "@/components/system/SystemMetricStrip";
import type { SystemActivitySnapshot } from "@/types/hub-events";
import type { HubConnectionState } from "@microsoft/signalr";
import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";

// p0209b: the System master/detail body. The selected subsystem comes from the
// route slug (resolved by app/system/[[...slug]]/page.tsx), so selection is
// URL-stable and survives refresh / deep-link by construction — no query param,
// no client selection state.
//   segment null                            → default subsystem (tracker)
//   tracker|webhooks|chat                    → SubsystemPage (KPI strip + stream)
//   config                                   → ConfigView (resolved-config sheet, then the read-events stream) (p0266)
//   catalog                                  → CatalogBrowser
// p0343d: every route renders as a first-class page in the parity vocabulary —
// the .mock-shell/.mock-system scope, an .m-head title row, a .health KPI strip
// where the page has real numbers, section rules + rows/cards below. Data,
// hooks and behaviors are unchanged; this is the re-dress layer only.
// Lives in components/ (not the page file) so the page exports only its default,
// satisfying Next's Page-type contract while staying unit-testable on the slug.

const SUBSYSTEM_IDS = SUBSYSTEMS.map((s) => s.id) as SubsystemId[];
const DEFAULT_SUBSYSTEM: SubsystemId = "tracker";

// 2026-08-27-1ed6: `installation` and `connections` left this view for /config — what an
// installation IS and whether its dependencies answer are configuration questions, not
// subsystems of the running system this view watches.
// 2026-08-27-7463: `cost`, `today` and `expectations` left it for /overview — a rollup is
// a reading OF the system, not a part of it, and three of them make one page.
// Page voice for the three event-stream subsystems (config/catalog carry their
// own heads below).
const STREAM_META: Record<"tracker" | "webhooks" | "chat", { title: TextKey; sub: TextKey }> = {
  tracker: { title: "system.stream.trackerTitle", sub: "system.stream.trackerSub" },
  webhooks: { title: "system.stream.webhooksTitle", sub: "system.stream.webhooksSub" },
  chat: { title: "system.stream.chatTitle", sub: "system.stream.chatSub" },
};

export function SystemView({ segment }: { segment: string | null }) {
  const t = useT();
  const { connectionState, systemActivity, refusal } = useJobsHub();

  const subsystem: SubsystemId =
    segment != null && (SUBSYSTEM_IDS as string[]).includes(segment)
      ? (segment as SubsystemId)
      : DEFAULT_SUBSYSTEM;

  // Read only the selected subsystem's scope from the shared store — the
  // detail pane no longer subscribes to the whole system firehose.
  const events = useSubsystemEvents(subsystem);
  const activity = useSubsystemActivity(events);

  return (
    <div className="mock-shell mock-system" data-testid="system-page">
      <main className="main">
        {subsystem === "catalog" ? (
          // p0221: the catalog subsystem is a system reference — it renders the
          // catalog's actual contents, not just its load-event stream.
          <>
            <PageHead
              title={t("system.stream.catalogTitle")}
              sub={t("system.stream.catalogSub")}
            />
            <CatalogBrowser />
          </>
        ) : subsystem === "config" ? (
          // p0266: the config subsystem renders the resolved-config sheet (the
          // config-time "how it's wired" view) above its read-events stream.
          <>
            <PageHead
              title={t("system.stream.configTitle")}
              sub={t("system.stream.configSub")}
              right={<ConnectionState state={connectionState} refusal={refusal} />}
            />
            <ConfigView activity={activity[subsystem]} />
          </>
        ) : (
          <SubsystemPage
            id={subsystem as "tracker" | "webhooks" | "chat"}
            activity={activity[subsystem]}
            snapshot={systemActivity}
            connectionState={connectionState}
            refusal={refusal}
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
  refusal,
}: {
  id: "tracker" | "webhooks" | "chat";
  activity: SubsystemActivity;
  snapshot: SystemActivitySnapshot | null;
  connectionState: HubConnectionState;
  refusal: ApiRefusal | null;
}) {
  const t = useT();
  const meta = STREAM_META[id];
  const cells = streamKpis(id, snapshot, t);
  return (
    <>
      <PageHead
        title={t(meta.title)}
        sub={t(meta.sub)}
        right={<ConnectionState state={connectionState} refusal={refusal} />}
      />
      {cells && <SystemMetricStrip testId={`system-kpis-${id}`} cells={cells} />}
      <SubsystemDetail activity={activity} heading={t("system.stream.eventStream")} />
    </>
  );
}

// "—" (not a fake 0) until the first server snapshot arrives.
function streamKpis(
  id: "tracker" | "webhooks" | "chat",
  snapshot: SystemActivitySnapshot | null,
  t: T,
): MetricCell[] | null {
  const v = (n: number) => (snapshot ? n : "—");
  switch (id) {
    case "tracker":
      return [
        { label: t("system.stream.ticketsScanned"), value: v(snapshot?.ticketsScanned ?? 0), testId: "sys-metric-tickets-scanned" },
        { label: t("system.stream.ticketsTriggered"), value: v(snapshot?.ticketsTriggered ?? 0), testId: "sys-metric-tickets-triggered" },
        { label: t("system.stream.ticketsSkipped"), value: v(snapshot?.ticketsSkipped ?? 0), testId: "sys-metric-tickets-skipped" },
        { label: t("system.stream.pollCycles"), value: v(snapshot?.pollCyclesFinished ?? 0), small: t("system.stream.day"), testId: "sys-metric-poll-cycles" },
      ];
    case "webhooks":
      return [
        { label: t("system.stream.received"), value: v(snapshot?.webhooksReceived ?? 0), small: t("system.stream.day"), testId: "sys-metric-webhooks-received" },
        { label: t("system.stream.actioned"), value: v(snapshot?.webhooksActioned ?? 0), small: t("system.stream.day"), testId: "sys-metric-webhooks-actioned" },
      ];
    case "chat":
      // No server-truth chat counters on the snapshot — no strip is honest.
      return null;
  }
}
