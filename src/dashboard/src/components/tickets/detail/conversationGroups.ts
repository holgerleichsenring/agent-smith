import { summaryLine } from "@/lib/formatRunSummary";
import { isRunLive } from "@/lib/runLiveness";
import type { TicketConversation, TicketConversationEntry, TicketDetail } from "@/types/tickets";
import { attemptPill } from "./conversationWords";

// 2026-10-17-e730c: the thread's groups as the Conversation tab draws them — before the first attempt, one per
// attempt, now — each with the entries the filter shows and whether it is open by default. With the All filter only
// the current attempt (the newest live one that has a group, else the newest) is open, with now, a group holding the
// ?causeOf= entry and an attempt waiting on a question; the before group is open when no attempt exists. A
// narrowing filter opens every group it found something in. Every choice reads a recorded field, never text.

export interface ConversationGroup {
  key: string;
  head: string;
  runId: string | null;
  pill: [string, string] | null;
  entries: TicketConversationEntry[];
  /** The attempt's end text in one line; null for none and for before / now. */
  cause: string | null;
  /** Before and attempt groups fold; now does not. */
  collapsible: boolean;
  openByDefault: boolean;
}

/** source: the filter's 9a59h source, null for All; target: the entry id ?causeOf= names, null for none. */
export function conversationGroups(
  conversation: TicketConversation, detail: TicketDetail, source: string | null, target: string | null,
): ConversationGroup[] {
  const shown = conversation.entries.filter((e) => source === null || e.source === source);
  const of = (runId: string) => detail.attempts.find((d) => d.runId === runId);
  const current = currentAttempt(conversation, detail);
  const narrowed = source !== null;
  const holds = (entries: TicketConversationEntry[]) => target !== null && entries.some((e) => e.id === target);

  const before = shown.filter((e) => !e.attemptRunId && e.group === "before");
  const groups: ConversationGroup[] = [{
    key: "before", head: "Before the first attempt", runId: null, pill: null, entries: before, cause: null,
    collapsible: true,
    openByDefault: narrowed ? before.length > 0 : conversation.attempts.length === 0 || holds(before),
  }];
  for (const a of conversation.attempts) {
    const attempt = of(a.runId);
    const entries = shown.filter((e) => e.attemptRunId === a.runId);
    groups.push({
      key: a.runId, head: `Attempt ${a.number}`, runId: a.runId, pill: attemptPill(attempt), entries,
      cause: summaryLine(attempt?.summary), collapsible: true,
      openByDefault: narrowed
        ? entries.length > 0
        : a.runId === current || attempt?.pendingQuestion != null || holds(entries),
    });
  }
  const now = shown.filter((e) => !e.attemptRunId && e.group === "now");
  groups.push({
    key: "now", head: "Now", runId: null, pill: ["q", "in the ticket conversation"], entries: now, cause: null,
    collapsible: false, openByDefault: true,
  });
  return groups.filter((g) => g.runId !== null || g.entries.length > 0);
}

/** The newest live attempt the conversation already has a group for, else the conversation's newest attempt. */
export function currentAttempt(conversation: TicketConversation, detail: TicketDetail): string | null {
  const grouped = new Set(conversation.attempts.map((a) => a.runId));
  const live = detail.attempts.filter((a) => isRunLive(a.status) && grouped.has(a.runId));
  if (live.length > 0) return live.reduce((n, a) => (a.number > n.number ? a : n)).runId;
  const newest = conversation.attempts.reduce<TicketConversation["attempts"][number] | null>(
    (n, a) => (n === null || a.number > n.number ? a : n), null);
  return newest?.runId ?? null;
}

/** "1 entry", "3 entries", "no entries". */
export function entryCount(n: number): string {
  if (n === 0) return "no entries";
  return n === 1 ? "1 entry" : `${n} entries`;
}
