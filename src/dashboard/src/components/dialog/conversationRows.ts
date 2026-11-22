import type { T } from "@/lib/i18n/translate";
import type { SpecDialogSessionSummary } from "@/types/spec-dialog";

// 2026-09-17-c7aed: the conversation list reads like a history, newest first, the way the list
// already arrives.
// 2026-09-21-f237c: and it is ONE list. The day headings — Today, Yesterday, Last week, Earlier —
// cost a line each and carried only what a per-row timestamp carries, and they were uneven by
// construction: a working day put everything under Today and left three of them unused, a Monday
// put everything under Last week. They are gone, and what they were dating moved onto the row.
// The module is named for what it does now: the strings a row is made of.

// Calendar days in the viewer's own time zone, so a conversation at 23:50 is yesterday at 00:10.
function calendarDaysBetween(at: Date, now: Date): number {
  const start = new Date(at.getFullYear(), at.getMonth(), at.getDate());
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((end.getTime() - start.getTime()) / 86_400_000);
}

/** What a conversation filed and how many tickets that made, in the list's words; null when it
 *  filed nothing. */
export function outcomeLabel(t: T, { outcome }: SpecDialogSessionSummary): string | null {
  if (!outcome) return null;
  const tickets = t("common.tickets", { count: outcome.tickets });
  const verb = t(outcome.partial ? "dialog.conversations.partlyFiled" : "dialog.conversations.filed");
  return outcome.kind ? `${outcome.kind} ${verb} · ${tickets}` : `${tickets} ${verb}`;
}

/** The time of day a conversation from today was last active; nothing for an older one. */
function timeOfDay(lastActivityAt: string, now: Date = new Date()): string | null {
  const at = new Date(lastActivityAt);
  if (calendarDaysBetween(at, now) !== 0) return null;
  return at.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

/**
 * 2026-09-21-f237b: when a conversation was last active, said relatively, for a row that has no
 * day heading over it to date it — which is every row on the conversations page, and, after
 * 2026-09-21-f237c, every row in the panel too. Today's rows keep the clock time, because on the
 * day you are working "14:20" is what tells two of them apart.
 */
export function lastActive(t: T, at: string, now: Date = new Date()): string {
  const days = calendarDaysBetween(new Date(at), now);
  if (days <= 0) return timeOfDay(at, now) ?? t("dialog.when.today");
  if (days === 1) return t("dialog.when.yesterday");
  if (days < 7) return t("dialog.when.daysAgo", { count: days });
  if (days < 30) return t("dialog.when.weeksAgo", { count: Math.floor(days / 7) });
  if (days < 365) return t("dialog.when.monthsAgo", { count: Math.floor(days / 30) });
  return t("dialog.when.yearsAgo", { count: Math.floor(days / 365) });
}

/** 2026-11-17-d762a: the colour an outcome's words take in the bar — a partial filing is amber, a whole one green. */
export function outcomeTone({ outcome }: SpecDialogSessionSummary): "ok" | "run" | "" {
  if (!outcome) return "";
  return outcome.partial ? "run" : "ok";
}
