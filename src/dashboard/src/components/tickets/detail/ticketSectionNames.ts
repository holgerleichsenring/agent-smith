// 2026-11-17-8d1ca: the names a ticket page address may carry for its section. ?section= names one; an older link's
// ?tab= still lands on the section that holds what that tab held (the attempts on Runs). Apart from the section
// table, so the address hook reads it without importing the panes.
// 2026-11-22-4321a: Now is the default and takes What happened's place (happened and summary land on it); the
// conversation is a section of its own, and an older address that carries only ?causeOf= lands there, at that entry.

export type TicketSectionId = "now" | "conversation" | "specs" | "acceptance" | "runs" | "cost" | "defaults";

export const SECTION_PARAM = "section";
export const TAB_PARAM = "tab";
export const DEFAULT_SECTION: TicketSectionId = "now";

const LANDS_ON: Record<string, TicketSectionId> = {
  now: "now", happened: "now", summary: "now", conversation: "conversation",
  specs: "specs", acceptance: "acceptance", runs: "runs", attempts: "runs", cost: "cost", defaults: "defaults",
};

/** The section a name lands on; an unknown or missing name is Now. */
export function sectionIdOf(name: string | null | undefined): TicketSectionId {
  return (name && LANDS_ON[name]) || DEFAULT_SECTION;
}
