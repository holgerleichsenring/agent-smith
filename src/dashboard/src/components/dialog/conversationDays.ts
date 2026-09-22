import type { SpecDialogSessionSummary } from "@/types/spec-dialog";

// 2026-09-17-c7aed: the conversation list reads like a history — grouped by the calendar day
// of the last thing said, newest first, the way the list already arrives.

export interface ConversationDay {
  label: string;
  conversations: SpecDialogSessionSummary[];
}

/** Groups conversations under Today, Yesterday, Last week and Earlier, keeping their order. */
export function groupByDay(
  conversations: SpecDialogSessionSummary[],
  now: Date = new Date(),
): ConversationDay[] {
  const days: ConversationDay[] = [];
  for (const conversation of conversations) {
    const label = dayLabel(new Date(conversation.lastActivityAt), now);
    const day = days.find((held) => held.label === label);
    if (day) day.conversations.push(conversation);
    else days.push({ label, conversations: [conversation] });
  }
  return days;
}

function dayLabel(at: Date, now: Date): string {
  const days = calendarDaysBetween(at, now);
  if (days <= 0) return "Today";
  if (days === 1) return "Yesterday";
  if (days <= 7) return "Last week";
  return "Earlier";
}

// Calendar days in the viewer's own time zone, so a conversation at 23:50 is yesterday at 00:10.
function calendarDaysBetween(at: Date, now: Date): number {
  const start = new Date(at.getFullYear(), at.getMonth(), at.getDate());
  const end = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  return Math.round((end.getTime() - start.getTime()) / 86_400_000);
}

/** What a conversation filed and how many tickets that made, in the list's words; null when it
 *  filed nothing. */
export function outcomeLabel({ outcome }: SpecDialogSessionSummary): string | null {
  if (!outcome) return null;
  const tickets = `${outcome.tickets} ticket${outcome.tickets === 1 ? "" : "s"}`;
  const verb = outcome.partial ? "partly filed" : "filed";
  return outcome.kind ? `${outcome.kind} ${verb} · ${tickets}` : `${tickets} ${verb}`;
}

/** The time of day for a conversation from today; nothing for an older one, whose group says when. */
export function timeOfDay(lastActivityAt: string, now: Date = new Date()): string | null {
  const at = new Date(lastActivityAt);
  if (calendarDaysBetween(at, now) !== 0) return null;
  return at.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

/**
 * 2026-09-21-f237b: when a conversation was last active, said relatively, for a row that has no
 * day heading over it to date it — which is every row on the conversations page, and, after
 * 2026-09-21-f237c, every row in the panel too.
 */
export function lastActive(at: string, now: Date = new Date()): string {
  const days = calendarDaysBetween(new Date(at), now);
  if (days <= 0) return timeOfDay(at, now) ?? "today";
  if (days === 1) return "yesterday";
  if (days < 7) return `${days} days ago`;
  if (days < 30) return `${Math.floor(days / 7)}w ago`;
  if (days < 365) return `${Math.floor(days / 30)}mo ago`;
  return `${Math.floor(days / 365)}y ago`;
}
