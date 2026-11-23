import type { ComponentType } from "react";
import type { TextKey } from "@/lib/i18n/translate";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { SeriesConversationSection } from "./SeriesConversationSection";
import { SeriesOverviewSection } from "./SeriesOverviewSection";
import { SeriesTicketsSection } from "./SeriesTicketsSection";
import { seriesOverviewCounts } from "./seriesNodeKind";
import type { SeriesMerge } from "./useSeriesMerge";

// 2026-11-23-31dcd: the series page's sections, ONE table, in the bar's order, each with the count the bar shows beside
// it; the page shows one at a time and ?section= names it, the first listed one without it.
// 2026-11-23-f562: three sections — Overview (the graph, its menus and the action list), Tickets, Conversation. Merge
// next, Who moves next and Order and branches are gone; their old names (next, held, course) land on the Overview.

export type SeriesSectionId = "overview" | "tickets" | "talk";

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
  /** reload reads the series again, after an act the pane started; merge is the page's one merge act (8d16). */
  Pane: ComponentType<{ detail: SeriesRunDetail; reload: () => void; merge?: SeriesMerge }>;
}

export const seriesSections: SeriesSection[] = [
  {
    id: "overview", label: "series.section.overview", Pane: SeriesOverviewSection,
    counter: (d) => { const yours = seriesOverviewCounts(d).yours; return { text: String(yours), hot: yours > 0 }; },
  },
  { id: "tickets", label: "series.section.tickets", Pane: SeriesTicketsSection, counter: (d) => ({ text: String(d.tickets.length) }) },
  { id: "talk", label: "series.section.conversation", Pane: SeriesConversationSection, counter: () => ({ text: "↗" }) },
];

/** The section an address names; a missing, unlisted or retired name (next, held, course) is the Overview. */
export function seriesSectionOf(name: string | null): SeriesSection {
  return seriesSections.find((section) => section.id === name) ?? seriesSections[0];
}
