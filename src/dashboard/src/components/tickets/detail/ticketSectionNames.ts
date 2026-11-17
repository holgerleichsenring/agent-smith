// 2026-11-17-8d1ca: the names a ticket page address may carry for its section. ?section= names one; an older link's
// ?tab= still lands on the section that holds what that tab held (the conversation and the summary on What happened,
// the attempts on Runs). Apart from the section table, so the address hook reads it without importing the panes.

export type TicketSectionId = "happened" | "specs" | "acceptance" | "runs" | "cost" | "defaults";

export const SECTION_PARAM = "section";
export const TAB_PARAM = "tab";

const LANDS_ON: Record<string, TicketSectionId> = {
  happened: "happened", conversation: "happened", summary: "happened",
  specs: "specs", acceptance: "acceptance", runs: "runs", attempts: "runs", cost: "cost", defaults: "defaults",
};

/** The section a name lands on; an unknown or missing name is What happened. */
export function sectionIdOf(name: string | null | undefined): TicketSectionId {
  return (name && LANDS_ON[name]) || "happened";
}
