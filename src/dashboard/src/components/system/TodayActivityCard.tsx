"use client";

import { useActivityKpis } from "@/hooks/useActivityKpis";
import type { SystemEvent } from "@/types/system-events";

interface Props {
  events: readonly SystemEvent[];
}

export function TodayActivityCard({ events }: Props) {
  const kpis = useActivityKpis(events);

  return (
    <section
      className="rounded-lg border border-stone-200 bg-white p-4"
      data-testid="today-activity-card"
    >
      <h2 className="mb-3 text-sm font-medium text-stone-700">Last 24h</h2>
      <dl className="grid grid-cols-2 gap-4 text-sm md:grid-cols-3">
        <Kpi label="Tickets scanned" value={kpis.ticketsScanned} testId="kpi-tickets-scanned" />
        <Kpi label="Tickets triggered" value={kpis.ticketsTriggered} testId="kpi-tickets-triggered" />
        <Kpi label="Tickets skipped" value={kpis.ticketsSkipped} testId="kpi-tickets-skipped" />
        <Kpi label="Webhooks received" value={kpis.webhooksReceived} testId="kpi-webhooks-received" />
        <Kpi label="Webhooks actioned" value={kpis.webhooksActioned} testId="kpi-webhooks-actioned" />
        <Kpi label="Poll cycles" value={kpis.pollCyclesFinished} testId="kpi-poll-cycles" />
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
