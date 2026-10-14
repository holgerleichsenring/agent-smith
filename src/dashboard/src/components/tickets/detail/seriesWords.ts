import type { TicketAttempt, TicketSeriesBudget } from "@/types/tickets";

// 2026-10-13-8762w: the words of the series strip. The server decides next and heldBecause; these say them. No
// sentence names Retry — Retry refuses every end a series hold follows.

export const GO_ON = "A comment on the ticket or an approval in its conversation starts the next attempt.";
export const SERIES_SETTINGS_PATH = "/config/settings/series";

/** What holds the series, how a person goes on, and where that sentence points when it points anywhere. */
export interface HeldWords {
  reason: string;
  goOn: string;
  to?: "defaults" | "settings";
}

const HELD_WORDS: Record<string, HeldWords> = {
  misconfigured: {
    reason: "the series settings are invalid (the ticket comment names the field)",
    goOn: "Fix them in the Series settings in Config Studio.", to: "settings",
  },
  "handed-back": { reason: "the last attempt handed the work back", goOn: GO_ON },
  cancelled: { reason: "the last attempt was cancelled", goOn: GO_ON },
  incomplete: { reason: "the last attempt ended without finishing", goOn: GO_ON },
  "no-progress": { reason: "the last attempt finished no spec", goOn: GO_ON },
  "budget-usd": { reason: "the chain of attempts has spent the series budget", goOn: GO_ON },
  "wall-clock": { reason: "the chain of attempts has used the series run time", goOn: GO_ON },
  "default-pending": { reason: "a default waits for a person", goOn: "Ratify it in the Defaults tab.", to: "defaults" },
  refused: { reason: "the last automatic start was refused", goOn: GO_ON },
  closed: { reason: "a person closed the ticket", goOn: GO_ON },
};

export function heldWords(code: string | null): HeldWords {
  return (code && HELD_WORDS[code]) || { reason: code ?? "held", goOn: GO_ON };
}

/** The next attempt in one phrase: by itself, held, or nothing left. */
export function nextWords(budget: TicketSeriesBudget): string {
  if (budget.next === "start") return "the next attempt starts by itself";
  if (budget.next === "complete") return "the series is complete";
  return `held — ${heldWords(budget.heldBecause).reason}`;
}

export interface ChainLink {
  number: number;
  /** Started by the series itself (cause channel series), not by a person or a trigger. */
  auto: boolean;
}

/** The chain, oldest first: the attempts from the chain's first to the newest. */
export function chainOf(attempts: TicketAttempt[], chainFrom: number): ChainLink[] {
  return attempts
    .filter((a) => a.number >= chainFrom)
    .map((a) => ({ number: a.number, auto: a.cause?.channel === "series" }));
}

export function hours(h: number): string {
  return `${Number.isInteger(h) ? h : h.toFixed(1)} h`;
}

/** The share of a cap used, 0..1; a zero cap is full. */
export function share(value: number, cap: number): number {
  return cap > 0 ? Math.min(1, Math.max(0, value / cap)) : 1;
}
