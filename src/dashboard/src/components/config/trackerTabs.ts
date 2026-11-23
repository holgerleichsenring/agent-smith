import type { CapabilityField } from "@/lib/configApi";
import type { T } from "@/lib/i18n/translate";
import { camelCase } from "./capabilityFields";

// 2026-10-06-cea8: the server says what a tracker field decides (its group); this is the one
// place that says which tab a group is drawn on. The form places fields by it, marks a tab by
// it, and the drawer's footer names the tab by it — one table, so the three cannot disagree.

/** The group whose fields are the run-outcome statuses — drawn as one block and compared. */
export const OUTCOME_GROUP = "outcome";

export const TRACKER_TABS = ["connection", "intake", "outcomes", "routing", "filing"] as const;
export type TrackerTab = (typeof TRACKER_TABS)[number];

const GROUP_TAB: Record<string, TrackerTab> = {
  connection: "connection",
  intake: "intake",
  outcome: "outcomes",
  transition: "outcomes",
  routing: "routing",
  filing: "filing",
};

/** A field with no group, or one this table does not know, lands on the first tab: a newly
 *  declared field is never hidden. `type` is not a descriptor field and belongs there too. */
export function trackerTabOf(field: CapabilityField | undefined): TrackerTab {
  return GROUP_TAB[field?.group ?? ""] ?? "connection";
}

/** The tab a finding naming `field` belongs to — the server may spell it snake_case. */
export function trackerTabFor(field: string, fields: CapabilityField[]): TrackerTab {
  const key = camelCase(field);
  return trackerTabOf(fields.find((f) => f.key === key));
}

const TAB_TITLE = {
  connection: "config.tracker.tab.connection",
  intake: "config.tracker.tab.intake",
  outcomes: "config.tracker.tab.outcomes",
  routing: "config.tracker.tab.routing",
  filing: "config.tracker.tab.filing",
} as const satisfies Record<TrackerTab, string>;

export function trackerTabTitle(t: T, tab: TrackerTab): string {
  return t(TAB_TITLE[tab]);
}
