import type { ComponentType } from "react";
import type { TextKey } from "@/lib/i18n/translate";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { SeriesConversationSection } from "./SeriesConversationSection";
import { SeriesOrderSection } from "./SeriesOrderSection";
import { SeriesMergeNextSection } from "./SeriesMergeNextSection";
import { SeriesHoldsSection } from "./SeriesHoldsSection";
import { SeriesTicketsSection } from "./SeriesTicketsSection";
import { mergesNow } from "./seriesStepLines";

// 2026-11-23-31dcd: the series page's sections, ONE table, in the bar's order — Merge next, What holds the rest, Order
// and branches, Tickets, Conversation — each with the count the bar shows beside it; the page shows one at a time and
// ?section= names it. Merge next (31dce), What holds the rest (31dcg) and Order and branches (31dcf) are named here and
// listed once their pane exists; the first listed section is the default. 2026-11-23-31dce: Merge next is listed.
// 2026-11-23-31dcg: and What holds the rest, counted by the server's held count. 2026-11-23-849e: renamed Who moves next
// (the id stays held, so links keep working).

export type SeriesSectionId = "next" | "held" | "course" | "tickets" | "talk";

export const SERIES_SECTION_PARAM = "section";

export interface SeriesSectionCounter {
  text: string;
  /** Drawn in the waits-for-a-person colour. */
  hot?: boolean;
}

export interface SeriesSection {
  id: SeriesSectionId;
  label: TextKey;
  counter: (detail: SeriesRunDetail) => SeriesSectionCounter | null;
  /** reload reads the series again, after an act the pane started. */
  Pane: ComponentType<{ detail: SeriesRunDetail; reload: () => void }>;
}

export const seriesSections: SeriesSection[] = [
  {
    id: "next", label: "series.section.next", Pane: SeriesMergeNextSection,
    counter: (d) => ({ text: String(d.mergeOrder.length), hot: d.mergeOrder.some((step) => mergesNow(d, step)) }), // 849e
  },
  {
    id: "held", label: "series.section.held", Pane: SeriesHoldsSection,
    counter: (d) => ({ text: String(d.counts.held), hot: d.counts.held > 0 }),
  },
  { id: "course", label: "series.section.order", Pane: SeriesOrderSection, counter: (d) => ({ text: String(d.tickets.length) }) }, // 31dcf
  { id: "tickets", label: "series.section.tickets", Pane: SeriesTicketsSection, counter: (d) => ({ text: String(d.tickets.length) }) },
  { id: "talk", label: "series.section.conversation", Pane: SeriesConversationSection, counter: () => ({ text: "↗" }) },
];

/** The section an address names; a missing or unlisted name is the first listed section. */
export function seriesSectionOf(name: string | null): SeriesSection {
  return seriesSections.find((section) => section.id === name) ?? seriesSections[0];
}
