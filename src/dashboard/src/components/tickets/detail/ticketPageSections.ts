import type { ComponentType } from "react";
import type { TicketDefaults, TicketDetail, TicketSeries } from "@/types/tickets";
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
}

export interface TicketSection {
  id: TicketSectionId;
  label: string;
  counter?: (detail: TicketDetail, reads: TicketSectionReads) => TicketSectionCounter | null;
  /** The section's pane; What happened is drawn by the page itself (the thread and its composer). */
  Pane: ComponentType | null;
}

export const ticketSections: TicketSection[] = [
  { id: "happened", label: "What happened", Pane: null },
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
      return tally ? { text: `${tally.verified}/${tally.total}`, ok: tally.total > 0 && tally.verified === tally.total } : null;
    },
  },
  { id: "runs", label: "Runs", Pane: TicketRunsSection, counter: (detail) => ({ text: String(detail.attempts.length) }) },
  {
    id: "cost", label: "Cost", Pane: TicketCostTab,
    counter: (detail) => ({ text: money(ticketCost(detail.attempts, detail.derivations ?? [])) }),
  },
  {
    // 2026-10-13-8762q: hot only while a pending default holds the series (a pending one never blocks).
    id: "defaults", label: "Defaults", Pane: TicketDefaultsTab,
    counter: (_detail, { defaults }) => defaults && defaults.entries.length > 0
      ? { text: String(defaults.pending), hot: defaults.pendingHoldingSeries > 0 }
      : null,
  },
];

/** The section the address shows: ?section= first, else an older ?tab=, else What happened. */
export function sectionOf(section: string | null, tab: string | null): TicketSection {
  const id = sectionIdOf(section ?? tab);
  return ticketSections.find((s) => s.id === id)!;
}
