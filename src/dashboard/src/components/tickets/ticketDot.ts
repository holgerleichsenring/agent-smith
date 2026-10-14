import type { TicketSummary } from "@/types/tickets";

// The ticket's state dot, one function for the list row's .sd and the header's .ticon so the same
// summary is drawn in the same colour on both. Palette: proven green, working amber, stopped red,
// waits-for-a-person blue.

/** Every class the dot can carry; each has a rule under .sd and under .t-head .ticon. */
export const TICKET_DOT_CLASSES = ["need", "run", "queued", "human", "ok", "bad", "idle"] as const;
export type TicketDotClass = (typeof TICKET_DOT_CLASSES)[number];

/** An ended ticket is coloured by its pull requests when all closed unmerged, else by how its last
 *  attempt ended — a success is never painted in the stopped colour. */
export function ticketDot(t: TicketSummary): TicketDotClass {
  switch (t.bucket) {
    case "needs_you":
      return "need";
    case "working":
      return t.status === "running" ? "run" : "queued";
    case "waiting_on_review":
      return "human";
    case "stuck":
      return "bad";
    case "done":
      return "ok";
    default:
      return t.landing === "unmerged" ? "bad" : outcomeDot(t.lastOutcome);
  }
}

function outcomeDot(outcome: string | null): TicketDotClass {
  if (outcome === "success" || outcome === "shortfall") return "ok";
  if (outcome === "failed" || outcome === "cancelled") return "bad";
  return "idle";
}
