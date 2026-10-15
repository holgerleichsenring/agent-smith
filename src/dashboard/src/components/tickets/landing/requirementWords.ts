import type { TicketRequirement } from "@/types/tickets";
import { age } from "../ticketFormat";

// 2026-10-14-dbcen: a requires entry in words — "(not merged)", "(done by hand)", "(merged)" — and, for a
// landing by hand, who marked it (By as the server stored it), when, and their note. 2026-10-15-114fd: an unlanded
// ticket a person closed reads "(closed)" — the one that waits for it still waits.

const LANDED_WORDS: Record<string, string> = { merged: "merged", "by-hand": "done by hand" };

export function isLanded(r: TicketRequirement): boolean {
  return !!r.landedKind;
}

/** What a waits-for entry says about its ticket. */
export function waitWords(r: TicketRequirement): string {
  if (r.landedKind) return `(${LANDED_WORDS[r.landedKind] ?? r.landedKind})`;
  return r.state === "closed" ? "(closed)" : "(not merged)";
}

/** What a required-by entry says about the ticket that waits. */
export function dependentWords(r: TicketRequirement): string {
  return `(${r.state.replace(/[-_]/g, " ")})`;
}

/** "by Ada Lovelace 3h ago — shipped by hand"; null unless a person marked it. */
export function landedByWords(r: TicketRequirement): string | null {
  if (r.landedKind !== "by-hand") return null;
  const when = age(r.landedAt);
  const by = r.landedBy ? `by ${r.landedBy}` : "by hand";
  return `${by}${when ? `, ${when === "now" ? "just now" : `${when} ago`}` : ""}${r.note ? ` — ${r.note}` : ""}`;
}
