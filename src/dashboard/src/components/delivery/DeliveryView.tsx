"use client";

import { useState } from "react";
import type { JobsHubClient } from "@/lib/JobsHubClient";
import type { DeliveryPeriod } from "@/lib/deliveryApi";
import { useDelivery } from "@/hooks/useDelivery";
import { PageHead } from "@/components/system/PageHead";
import { DeliveryChips, DeliveryPeriodSwitch } from "./DeliveryScope";
import { DeliveryKpis } from "./DeliveryKpis";
import { DeliveryTimeline } from "./DeliveryTimeline";
import { DeliveryEndCauses } from "./DeliveryEndCauses";
import { DeliveryProjects } from "./DeliveryProjects";
import { DeliveryTickets } from "./DeliveryTickets";

// 2026-10-25-0099c: the Delivery page (design/mockups/delivery-overview.html) — what was worked on when, how long it
// took, how it ended and the key figures, for a day, a week or a month, in the projects the caller may read. The
// chips are the server's list of those projects; a chip switched off reads the period again without it.

export function DeliveryView({ client }: { client?: JobsHubClient }) {
  const [period, setPeriod] = useState<DeliveryPeriod>("week");
  const [off, setOff] = useState<ReadonlySet<string>>(new Set());
  const [chips, setChips] = useState<readonly string[]>([]);
  const chosen = off.size === 0 ? [] : chips.filter((p) => !off.has(p));
  const { view, failed } = useDelivery(period, chosen, client);
  if (view && view.projects.join("\n") !== chips.join("\n")) setChips(view.projects);
  const toggle = (project: string) => setOff((was) => {
    const next = new Set(was);
    if (!next.delete(project)) next.add(project);
    return next;
  });

  return (
    <div className="mock-shell mock-system mock-delivery" data-testid="delivery-page">
      <main className="main">
        <PageHead
          title="Delivery"
          sub="The tickets the agent worked on in this period. Each row is a ticket and each bar is one attempt. Hover a bar for its duration, cost and why it ended."
          right={<DeliveryPeriodSwitch period={period} onPeriod={setPeriod} />}
        />
        {failed && !view && <p className="sub" data-testid="delivery-failed">The delivery overview could not be read.</p>}
        {!view && !failed && <p className="sub">Reading the period.</p>}
        {view && (
          <>
            <DeliveryChips projects={view.projects} off={off} onToggle={toggle} />
            <DeliveryKpis current={view.current} previous={view.previous} slots={view.slots} period={view.period} truncated={view.truncated} />
            <DeliveryTimeline tickets={view.tickets} from={view.from} to={view.to} period={view.period} />
            <div className="grid2">
              <DeliveryEndCauses causes={view.endCauses} />
              <DeliveryProjects projects={view.byProject} />
            </div>
            <DeliveryTickets tickets={view.tickets} />
          </>
        )}
      </main>
    </div>
  );
}
