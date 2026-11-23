import type { ComponentType } from "react";
import { translator, type T, type TextKey } from "@/lib/i18n/translate";
import type { TicketConversation, TicketDefaults, TicketDetail, TicketSeries } from "@/types/tickets";
import { money, ticketCost } from "../ticketFormat";
import { specsCounter, acceptanceTally } from "./specWords";
import { TicketAcceptanceTab } from "./TicketAcceptanceTab";
import { TicketRunsSection } from "./TicketRunsSection";
import { TicketCostTab } from "./TicketCostTab";
import { TicketDefaultsTab } from "./TicketDefaultsTab";
import { TicketSpecsTab } from "./TicketSpecsTab";
import { sectionIdOf, type TicketSectionId } from "./ticketSectionNames";

export { SECTION_PARAM, sectionIdOf, type TicketSectionId } from "./ticketSectionNames";

// 2026-10-13-9a59q: the ticket page's tabs, ONE table. 2026-11-17-8d1ca: they are the sections of the ticket's own
// bar now — What happened first, then Specs, Acceptance, Runs, Cost and Defaults — each with the count the bar shows
// beside it, and the page shows one at a time. ?section= names it; an older ?tab= link still lands on its section.
// 2026-11-22-4321a: Now takes What happened's place at the top, and the conversation, with its count of entries,
// is the section after it. An address with only ?causeOf= (an older "read" link) lands on the conversation.

export interface TicketSectionCounter {
  text: string;
  /** Drawn in the waits-for-a-person colour. */
  hot?: boolean;
  /** Drawn in the passed colour: every criterion verified. */
  ok?: boolean;
}

/** The page's other reads, for counters that count more than the detail. */
export interface TicketSectionReads {
  series: TicketSeries | null;
  defaults: TicketDefaults | null;
  conversation: TicketConversation | null;
}

export interface TicketSection {
  id: TicketSectionId;
  /** 2026-11-23-5669b: a dictionary key; the page looks it up in the chosen language. */
  label: TextKey;
  counter?: (detail: TicketDetail, reads: TicketSectionReads, t?: T) => TicketSectionCounter | null;
  /** The section's pane; Now and Conversation are drawn by the page itself (each holds the composer). */
  Pane: ComponentType | null;
}

export const ticketSections: TicketSection[] = [
  { id: "now", label: "tickets.pageSection.now", Pane: null },
  {
    id: "conversation", label: "tickets.pageSection.conversation", Pane: null,
    counter: (_detail, { conversation }) => (conversation ? { text: String(conversation.entries.length) } : null),
  },
  {
    id: "specs", label: "tickets.pageSection.specs", Pane: TicketSpecsTab,
    counter: (detail, { series }, t = translator("en")) => {
      const text = specsCounter(t, detail, series);
      return text ? { text } : null;
    },
  },
  {
    id: "acceptance", label: "tickets.pageSection.acceptance", Pane: TicketAcceptanceTab,
    counter: (_detail, { series }) => {
      const tally = acceptanceTally(series);
      return tally ? { text: `${tally.verified}/${tally.total}`, ok: tally.total > 0 && tally.verified === tally.total } : null;
    },
  },
  { id: "runs", label: "tickets.pageSection.runs", Pane: TicketRunsSection, counter: (detail) => ({ text: String(detail.attempts.length) }) },
  {
    id: "cost", label: "tickets.pageSection.cost", Pane: TicketCostTab,
    counter: (detail) => ({ text: money(ticketCost(detail.attempts, detail.derivations ?? [])) }),
  },
  {
    // 2026-10-13-8762q: hot only while a pending default holds the series (a pending one never blocks).
    id: "defaults", label: "tickets.pageSection.defaults", Pane: TicketDefaultsTab,
    counter: (_detail, { defaults }) => defaults && defaults.entries.length > 0
      ? { text: String(defaults.pending), hot: defaults.pendingHoldingSeries > 0 }
      : null,
  },
];

/** The section the address shows: ?section= first, else an older ?tab=, else the conversation when ?causeOf= names
 *  an entry to show, else Now. */
export function sectionOf(section: string | null, tab: string | null, causeOf: string | null = null): TicketSection {
  const id = sectionIdOf(section ?? tab ?? (causeOf ? "conversation" : null));
  return ticketSections.find((s) => s.id === id)!;
}
