"use client";

import { useTicketConversation, useTicketSeries } from "@/lib/tickets/TicketPageStore";
import type { TicketDetail } from "@/types/tickets";
import { ticketTabs, type TicketTab } from "./ticketTabTable";

// 2026-10-13-9a59q: the mock's .tabs bar — role=tablist, one tab per ticketTabs row with its counter.

interface Props {
  detail: TicketDetail;
  selected: TicketTab;
  onSelect: (id: string) => void;
}

export function TicketTabs({ detail, selected, onSelect }: Props) {
  const reads = { conversation: useTicketConversation().data, series: useTicketSeries().data };
  return (
    <div className="tabs" role="tablist" aria-label="Ticket sections">
      {ticketTabs.map((tab) => {
        const counter = tab.counter?.(detail, reads) ?? null;
        return (
          <button
            key={tab.id}
            type="button"
            className="tab"
            role="tab"
            id={`t-${tab.id}`}
            aria-selected={tab.id === selected.id}
            aria-controls={`tab-${tab.id}`}
            onClick={() => onSelect(tab.id)}
            data-testid={`ticket-tab-${tab.id}`}
          >
            {tab.label}
            {counter && <span className={counter.hot ? "tc hot" : "tc"}>{counter.text}</span>}
          </button>
        );
      })}
    </div>
  );
}
