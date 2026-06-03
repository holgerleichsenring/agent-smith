"use client";

import type { SystemActivitySnapshot } from "@/types/hub-events";
import type { Kpi } from "@/components/system/RollupCards";

interface Props {
  activity: SystemActivitySnapshot | null;
}

// p0209c: the rollup card grid (RollupCards) reuses these same counters,
// re-presented as the mockup's .kcard grid. All six come straight off the
// server-truth SystemActivitySnapshot — no new backend, no new aggregation.
export function activityKpis(activity: SystemActivitySnapshot | null): Kpi[] {
  return [
    { label: "Tickets scanned", value: activity?.ticketsScanned ?? 0, testId: "kcard-tickets-scanned" },
    { label: "Tickets triggered", value: activity?.ticketsTriggered ?? 0, testId: "kcard-tickets-triggered" },
    { label: "Tickets skipped", value: activity?.ticketsSkipped ?? 0, testId: "kcard-tickets-skipped" },
    { label: "Poll cycles", value: activity?.pollCyclesFinished ?? 0, testId: "kcard-poll-cycles" },
    { label: "Webhooks received", value: activity?.webhooksReceived ?? 0, testId: "kcard-webhooks-received" },
    { label: "Webhooks actioned", value: activity?.webhooksActioned ?? 0, testId: "kcard-webhooks-actioned" },
  ];
}

// p0175-fix: reads the server-computed 24h rollup. Old client-derived
// useActivityKpis path was capped by the local 500-event ring buffer
// and drifted from the visible cycle list when the buffer filled.

export function TodayActivityCard({ activity }: Props) {
  return (
    <section
      className="rounded-lg border border-stone-200 bg-white p-4"
      data-testid="today-activity-card"
    >
      <h2 className="mb-3 text-sm font-medium text-stone-700">Last 24h</h2>
      <dl className="grid grid-cols-2 gap-4 text-sm md:grid-cols-3">
        <Kpi label="Tickets scanned" value={activity?.ticketsScanned ?? 0} testId="kpi-tickets-scanned" />
        <Kpi label="Tickets triggered" value={activity?.ticketsTriggered ?? 0} testId="kpi-tickets-triggered" />
        <Kpi label="Tickets skipped" value={activity?.ticketsSkipped ?? 0} testId="kpi-tickets-skipped" />
        <Kpi label="Webhooks received" value={activity?.webhooksReceived ?? 0} testId="kpi-webhooks-received" />
        <Kpi label="Webhooks actioned" value={activity?.webhooksActioned ?? 0} testId="kpi-webhooks-actioned" />
        <Kpi label="Poll cycles" value={activity?.pollCyclesFinished ?? 0} testId="kpi-poll-cycles" />
      </dl>
    </section>
  );
}

function Kpi({ label, value, testId }: { label: string; value: number; testId: string }) {
  return (
    <div data-testid={testId}>
      <dt className="text-xs text-stone-500">{label}</dt>
      <dd className="text-2xl font-medium tabular-nums text-stone-800">{value}</dd>
    </div>
  );
}
