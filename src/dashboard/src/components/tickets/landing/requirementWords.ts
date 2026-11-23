import type { T, TextKey } from "@/lib/i18n/translate";
import type { TicketRequirement } from "@/types/tickets";
import { ago } from "../ticketFormat";

// 2026-10-14-dbcen: a requires entry in words — "(not merged)", "(done by hand)", "(merged)" — and, for a
// landing by hand, who marked it (By as the server stored it), when, and their note. 2026-10-15-114fd: an unlanded
// ticket a person closed reads "(closed)" — the one that waits for it still waits.

const LANDED_KEYS: Record<string, TextKey> = { merged: "tickets.landing.merged", "by-hand": "tickets.landing.byHandDone" };

/** 2026-11-23-5669b: the server buckets a required-by entry names, as words; an unknown one is shown as sent. */
const DEPENDENT_KEYS: Record<string, TextKey> = {
  needs_you: "tickets.landing.state.needs_you", working: "tickets.landing.state.working", stuck: "tickets.landing.state.stuck",
  blocked: "tickets.landing.state.blocked", filed: "tickets.landing.state.filed", pr_closed: "tickets.landing.state.pr_closed",
  failed: "tickets.landing.state.failed", idle: "tickets.landing.state.idle", done: "tickets.landing.state.done",
  closed: "tickets.landing.state.closed",
};

export function isLanded(r: TicketRequirement): boolean {
  return !!r.landedKind;
}

/** What a waits-for entry says about its ticket. */
export function waitWords(t: T, r: TicketRequirement): string {
  if (r.landedKind) return `(${LANDED_KEYS[r.landedKind] ? t(LANDED_KEYS[r.landedKind]) : r.landedKind})`;
  return `(${r.state === "closed" ? t("tickets.landing.closed") : t("tickets.landing.notMerged")})`;
}

/** What a required-by entry says about the ticket that waits. */
export function dependentWords(t: T, r: TicketRequirement): string {
  const key = DEPENDENT_KEYS[r.state];
  if (key) return `(${t(key)})`;
  return `(${r.state.replace(/[-_]/g, " ")})`;
}

/** "by Ada Lovelace 3h ago — shipped by hand"; null unless a person marked it. */
export function landedByWords(t: T, r: TicketRequirement): string | null {
  if (r.landedKind !== "by-hand") return null;
  const when = ago(t, r.landedAt);
  const by = r.landedBy ? t("tickets.landing.by", { by: r.landedBy }) : t("tickets.landing.byHand");
  return `${by}${when ? `, ${when}` : ""}${r.note ? ` — ${r.note}` : ""}`;
}
