"use client";

import { useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketDetail } from "@/types/tickets";
import { SeriesRunway } from "./SeriesRunway";
import { TicketTabs } from "./TicketTabs";
import { TAB_PARAM, ticketTab } from "./ticketTabTable";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-11-16-6877a: everything the old tabs showed beside the conversation, folded at the end of the thread. Closed
// by default; ?tab= naming one of its panes opens it on that pane, so every older link (Open specs, the runway's
// defaults, a spec's id) still lands where it pointed.

export function TicketDetails({ detail }: { detail: TicketDetail }) {
  const budget = useTicketSeries().data?.budget;
  const address = useTicketTabGo();
  const tab = ticketTab(address.param(TAB_PARAM));
  const select = (id: string) => address.go(id, { [CAUSE_PARAM]: null });
  return (
    <section className="tk-details" data-testid="ticket-details">
      <button type="button" className="tk-details-head" aria-expanded={tab !== null} aria-controls="ticket-details-body"
        onClick={() => (tab ? address.go(null) : select("summary"))} data-testid="ticket-details-toggle">
        <span className="tk-chev" aria-hidden="true">{tab ? "▾" : "▸"}</span>
        Details
        <span className="tk-dim">the ticket, specs, acceptance, attempts, cost and defaults</span>
      </button>
      {tab && (
        <div id="ticket-details-body">
          <SeriesRunway budget={budget} attempts={detail.attempts} onDefaults={() => select("defaults")} />
          <TicketTabs detail={detail} selected={tab} onSelect={select} />
          <section className="tabpane" id={`tab-${tab.id}`} role="tabpanel" aria-labelledby={`t-${tab.id}`}>
            <tab.Pane />
          </section>
        </div>
      )}
    </section>
  );
}
