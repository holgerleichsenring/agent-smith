import type { T, TextKey } from "@/lib/i18n/translate";
import type { TicketAttempt, TicketSeriesBudget } from "@/types/tickets";

// 2026-10-13-8762w: the words of the series strip. The server decides next and heldBecause; these say them. No
// sentence names Retry — Retry refuses every end a series hold follows.

/** 2026-11-23-5669b: the go-on sentence's dictionary key. */
export const GO_ON: TextKey = "tickets.series.goOn";
export const SERIES_SETTINGS_PATH = "/config/settings/series";

/** What holds the series, how a person goes on, and where that sentence points when it points anywhere. */
export interface HeldWords {
  reason: string;
  goOn: string;
  to?: "defaults" | "settings";
}

interface HeldKeys {
  reason: TextKey;
  goOn: TextKey;
  to?: "defaults" | "settings";
}

const HELD_WORDS: Record<string, HeldKeys> = {
  misconfigured: { reason: "tickets.series.held.misconfigured", goOn: "tickets.series.goOnSettings", to: "settings" },
  "handed-back": { reason: "tickets.series.held.handed-back", goOn: GO_ON },
  cancelled: { reason: "tickets.series.held.cancelled", goOn: GO_ON },
  incomplete: { reason: "tickets.series.held.incomplete", goOn: GO_ON },
  "no-progress": { reason: "tickets.series.held.no-progress", goOn: GO_ON },
  "budget-usd": { reason: "tickets.series.held.budget-usd", goOn: GO_ON },
  "wall-clock": { reason: "tickets.series.held.wall-clock", goOn: GO_ON },
  "default-pending": { reason: "tickets.series.held.default-pending", goOn: "tickets.series.goOnDefaults", to: "defaults" },
  refused: { reason: "tickets.series.held.refused", goOn: GO_ON },
  closed: { reason: "tickets.series.held.closed", goOn: GO_ON },
};

export function heldWords(t: T, code: string | null): HeldWords {
  const keys = code ? HELD_WORDS[code] : undefined;
  if (!keys) return { reason: code ?? t("tickets.series.held"), goOn: t(GO_ON) };
  return { reason: t(keys.reason), goOn: t(keys.goOn), ...(keys.to ? { to: keys.to } : {}) };
}

/** The next attempt in one phrase: by itself, held, or nothing left. */
export function nextWords(t: T, budget: TicketSeriesBudget): string {
  if (budget.next === "start") return t("tickets.series.next.start");
  if (budget.next === "complete") return t("tickets.series.next.complete");
  return t("tickets.series.next.held", { reason: heldWords(t, budget.heldBecause).reason });
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

export function hours(h: number, locale = "en-GB"): string {
  return `${Number.isInteger(h) ? h : h.toLocaleString(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 })} h`;
}

/** The share of a cap used, 0..1; a zero cap is full. */
export function share(value: number, cap: number): number {
  return cap > 0 ? Math.min(1, Math.max(0, value / cap)) : 1;
}
