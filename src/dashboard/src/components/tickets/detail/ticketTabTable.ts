import type { ComponentType } from "react";
import type { TicketConversation, TicketDetail, TicketSeries } from "@/types/tickets";
import { money } from "../ticketFormat";
import { specsCounter, acceptanceTally } from "./specWords";
import { TicketAcceptanceTab } from "./TicketAcceptanceTab";
import { TicketAttemptsTab } from "./TicketAttemptsTab";
import { TicketConversationTab } from "./TicketConversationTab";
import { TicketCostTab } from "./TicketCostTab";
import { TicketSpecsTab } from "./TicketSpecsTab";
import { TicketSummaryTab } from "./TicketSummaryTab";

export { TAB_PARAM } from "./useTicketTabGo";

// 2026-10-13-9a59q: the ticket page's tabs, ONE table. 2026-10-13-9a59r and 2026-10-13-9a59s append
// their tabs and counters here, so the bar never shows a tab without its pane.

export interface TicketTabCounter {
  text: string;
  /** Drawn in the waits-for-a-person colour. */
  hot?: boolean;
}

/** 2026-10-13-9a59r: the page's other reads, for counters that count more than the detail. */
export interface TicketTabReads {
  conversation: TicketConversation | null;
  series: TicketSeries | null;
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
    id: "conversation", label: "Conversation", Pane: TicketConversationTab,
    counter: (detail, { conversation }) => conversation
      ? { text: String(conversation.entries.length), hot: detail.attempts.some((a) => a.pendingQuestion) }
      : null,
  },
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
    counter: (detail) => ({ text: money(detail.attempts.reduce((sum, a) => sum + a.cost, 0)) }),
  },
];

export function ticketTab(id: string | null): TicketTab {
  return ticketTabs.find((t) => t.id === id) ?? ticketTabs[0];
}
