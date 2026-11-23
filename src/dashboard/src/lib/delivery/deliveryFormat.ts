import type { DeliveryFigures, DeliveryOutcome, DeliveryPeriod } from "@/lib/deliveryApi";
import { dateLocale } from "@/lib/i18n/dates";
import { translator, type Language, type T, type TextKey } from "@/lib/i18n/translate";

// 2026-10-25-0099c: how the Delivery page writes its numbers and words. Copy follows the approved mock
// (design/mockups/delivery-overview.html); an empty value is "-", never an em dash.
// 2026-11-23-5669f: the words come from the dictionary and the decimals from the chosen language's locale.

/** The lookup and the number locale of one language: what every helper here words its text with. */
export interface Wording {
  t: T;
  locale: string;
}

export function wording(language: Language): Wording {
  return { t: translator(language), locale: dateLocale(language) };
}

export const NONE = "-";
const MINUS = "−";

/** `value` with exactly `digits` decimals, in the wording's locale and without grouping, as toFixed would in English. */
function fixed(w: Wording, value: number, digits: number): string {
  return new Intl.NumberFormat(w.locale, { minimumFractionDigits: digits, maximumFractionDigits: digits, useGrouping: false }).format(value);
}

export function formatDuration(w: Wording, seconds: number): string {
  if (seconds < 3600) return w.t("delivery.minutes", { value: Math.round(seconds / 60) });
  if (seconds < 48 * 3600) return w.t("delivery.hours", { value: fixed(w, seconds / 3600, 1) });
  return w.t("delivery.days", { value: fixed(w, seconds / 86400, 1) });
}

export const formatUsd = (w: Wording, value: number) => w.t("delivery.usd", { value: fixed(w, value, 2) });
export const formatRate = (w: Wording, value: number) => w.t("delivery.percent", { value: Math.round(value * 100) });

export const comparedWith = (t: T, period: DeliveryPeriod) => t(`delivery.compared.${period}`);

type Kind = "count" | "rate" | "duration" | "money" | "ratio";

export interface Kpi {
  /** The figure's stable name, also its test id. */
  id: string;
  label: TextKey;
  value: (f: DeliveryFigures) => number | null;
  kind: Kind;
  /** Whether a higher value is the better one. */
  higherIsBetter: boolean;
}

export const KPIS: readonly Kpi[] = [
  { id: "ticketsWorked", label: "delivery.kpi.ticketsWorked", value: (f) => f.ticketsWorked, kind: "count", higherIsBetter: true },
  { id: "merged", label: "delivery.kpi.merged", value: (f) => f.merged, kind: "count", higherIsBetter: true },
  { id: "mergeRate", label: "delivery.kpi.mergeRate", value: (f) => f.mergeRate, kind: "rate", higherIsBetter: true },
  { id: "medianTimeToMerge", label: "delivery.kpi.medianTimeToMerge", value: (f) => f.medianTimeToMergeSeconds, kind: "duration", higherIsBetter: false },
  { id: "reworkPerTicket", label: "delivery.kpi.reworkPerTicket", value: (f) => f.reworkPerTicket, kind: "ratio", higherIsBetter: false },
  { id: "costPerMerged", label: "delivery.kpi.costPerMerged", value: (f) => f.costPerMergedUsd, kind: "money", higherIsBetter: false },
  { id: "spend", label: "delivery.kpi.spend", value: (f) => f.spendUsd, kind: "money", higherIsBetter: false },
  { id: "agentHours", label: "delivery.kpi.agentHours", value: (f) => f.agentSeconds, kind: "duration", higherIsBetter: false },
];

export function formatValue(w: Wording, kind: Kind, value: number | null): string {
  if (value === null) return NONE;
  switch (kind) {
    case "count": return String(value);
    case "rate": return formatRate(w, value);
    case "duration": return formatDuration(w, value);
    case "money": return formatUsd(w, value);
    case "ratio": return fixed(w, value, 2);
  }
}

export interface Delta {
  text: string;
  /** True when the change is an improvement, false when it is worse, null when nothing changed. */
  better: boolean | null;
}

/** The change against the previous period; null when either side has no value. */
export function formatDelta(w: Wording, kpi: Kpi, current: number | null, previous: number | null): Delta | null {
  if (current === null || previous === null) return null;
  const diff = current - previous;
  if (Math.abs(diff) < 1e-9) return { text: w.t("delivery.delta.none"), better: null };
  const sign = diff > 0 ? "+" : MINUS;
  const size = Math.abs(diff);
  const text = kpi.kind === "rate"
    ? w.t("delivery.delta.points", { sign, value: Math.round(size * 100) })
    : `${sign}${formatValue(w, kpi.kind, size)}`;
  return { text, better: diff > 0 === kpi.higherIsBetter };
}

export const outcomeLabel = (t: T, outcome: DeliveryOutcome) => t(`delivery.outcome.${outcome}`);

/** Every cause GET /api/delivery sends: the run end kinds and the status words. */
export const CAUSES = [
  "success", "shortfall", "criteria-unmet", "preflight", "bootstrap-missing", "image-untrusted", "readiness-unknown",
  "superseded", "cancelled", "failed",
] as const;

type Cause = (typeof CAUSES)[number];

/** The colour an end cause is drawn in — the outcome it stands for. */
export const CAUSE_OUTCOME: Readonly<Record<string, DeliveryOutcome>> = {
  success: "delivered",
  shortfall: "shortfall",
  "criteria-unmet": "shortfall",
  superseded: "cancelled",
  cancelled: "cancelled",
};

/** A cause the page knows in words; one it does not is shown as the server sent it. */
export const causeLabel = (t: T, cause: string) =>
  (CAUSES as readonly string[]).includes(cause) ? t(`delivery.cause.${cause as Cause}`) : cause;
export const causeOutcome = (cause: string): DeliveryOutcome => CAUSE_OUTCOME[cause] ?? "failed";

/** A UTC instant as the page writes it: "10-25 18:00". */
export const formatAt = (iso: string) => iso.slice(5, 16).replace("T", " ");

/** 2026-11-12-aeddb: the span a period covers, "11-05 → 11-12"; a day's carries its hours, "11-11 14:00 → 11-12 14:00". */
export function formatSpan(period: DeliveryPeriod, from: string, to: string): string {
  const at = period === "day" ? formatAt : (iso: string) => iso.slice(5, 10);
  return `${at(from)} → ${at(to)}`;
}

/** What the newest attempt worked on: its specs, the repositories it changed and its first goal. */
export function formatWork(t: T, specTitles: readonly string[], repos: readonly string[]): string {
  const parts = [t("delivery.specs", { count: specTitles.length })];
  if (repos.length > 0) parts.push(t("delivery.repos", { count: repos.length }));
  if (specTitles.length > 0) parts.push(specTitles[0]);
  return specTitles.length === 0 && repos.length === 0 ? NONE : parts.join(" · ");
}
