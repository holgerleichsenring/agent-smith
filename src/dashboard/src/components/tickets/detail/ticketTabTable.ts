import type { ComponentType } from "react";
import type { TicketConversation, TicketDefaults, TicketDetail, TicketSeries } from "@/types/tickets";
import { money, ticketCost } from "../ticketFormat";
import { specsCounter, acceptanceTally } from "./specWords";
import { TicketAcceptanceTab } from "./TicketAcceptanceTab";
import { TicketAttemptsTab } from "./TicketAttemptsTab";
import { TicketCostTab } from "./TicketCostTab";
import { TicketDefaultsTab } from "./TicketDefaultsTab";
import { TicketSpecsTab } from "./TicketSpecsTab";
import { TicketSummaryTab } from "./TicketSummaryTab";

export { TAB_PARAM } from "./useTicketTabGo";

// 2026-10-13-9a59q: the ticket page's tabs, ONE table. 2026-10-13-9a59r and 2026-10-13-9a59s append
// their tabs and counters here, so the bar never shows a tab without its pane.
// 2026-11-16-6877a: the conversation is the page itself now; these are the panes the Details fold holds.

export interface TicketTabCounter {
  text: string;
  /** Drawn in the waits-for-a-person colour. */
  hot?: boolean;
}

/** 2026-10-13-9a59r: the page's other reads, for counters that count more than the detail. */
export interface TicketTabReads {
  conversation: TicketConversation | null;
  series: TicketSeries | null;
  /** 2026-10-13-8762q */
  defaults: TicketDefaults | null;
}

export interface TicketTab {
  id: string;
  label: string;
  counter?: (detail: TicketDetail, reads: TicketTabReads) => TicketTabCounter | null;
  Pane: ComponentType;
}

export const ticketTabs: TicketTab[] = [
  { id: "summary", label: "Summary", Pane: TicketSummaryTab },
  {
    id: "specs", label: "Specs", Pane: TicketSpecsTab,
    counter: (detail, { series }) => {
      const text = specsCounter(detail, series);
      return text ? { text } : null;
    },
  },
  {
    id: "acceptance", label: "Acceptance", Pane: TicketAcceptanceTab,
    counter: (_detail, { series }) => {
      const tally = acceptanceTally(series);
      return tally ? { text: `${tally.verified}/${tally.total}` } : null;
    },
  },
  { id: "attempts", label: "Attempts", Pane: TicketAttemptsTab, counter: (detail) => ({ text: String(detail.attempts.length) }) },
  {
    id: "cost", label: "Cost & compute", Pane: TicketCostTab,
    counter: (detail) => ({ text: money(ticketCost(detail.attempts, detail.derivations ?? [])) }),
  },
  {
    // 2026-10-13-8762q: appended last; hot only while a pending default holds the series (a pending one never blocks).
    id: "defaults", label: "Defaults", Pane: TicketDefaultsTab,
    counter: (_detail, { defaults }) => defaults && defaults.entries.length > 0
      ? { text: String(defaults.pending), hot: defaults.pendingHoldingSeries > 0 }
      : null,
  },
];

/** The Details pane ?tab= names; null when it names none (the fold is closed). */
export function ticketTab(id: string | null): TicketTab | null {
  return ticketTabs.find((t) => t.id === id) ?? null;
}
