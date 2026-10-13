import type { ComponentType } from "react";
import type { TicketDetail } from "@/types/tickets";
import { TicketSummaryTab } from "./TicketSummaryTab";

// 2026-10-13-9a59q: the ticket page's tabs, ONE table. 2026-10-13-9a59r and 2026-10-13-9a59s append
// their tabs and counters here, so the bar never shows a tab without its pane.

export interface TicketTabCounter {
  text: string;
  /** Drawn in the waits-for-a-person colour. */
  hot?: boolean;
}

export interface TicketTab {
  id: string;
  label: string;
  counter?: (detail: TicketDetail) => TicketTabCounter | null;
  Pane: ComponentType;
}

export const ticketTabs: TicketTab[] = [
  { id: "summary", label: "Summary", Pane: TicketSummaryTab },
];

export const TAB_PARAM = "tab";

export function ticketTab(id: string | null): TicketTab {
  return ticketTabs.find((t) => t.id === id) ?? ticketTabs[0];
}
