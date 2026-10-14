import type { AttemptCause, TicketConversationEntry } from "@/types/tickets";

// 2026-10-13-9a59r: the entry that IS an attempt's cause, joined on 9a59d's cause.source triple — never
// on 9a59h's composite id (a row key, not the cause's id) and never on time. source: a ticket comment
// cause (kind comment) is a ticket entry, a pull-request cause (review, comment, command) a pr-review
// entry; url: the pull request's url (the entry carries the record's readable url, the one its prKey
// hashes) or, for a ticket comment, none or the ticket's own; id: the entry's sourceId.

const PR_KINDS = new Set(["review", "comment", "command"]);

export function causeEntryMatch(
  entry: TicketConversationEntry, cause: AttemptCause | null | undefined, ticketUrl: string | null = null,
): boolean {
  const source = cause?.source;
  if (!cause || !source?.id || !entry.sourceId || entry.sourceId !== source.id) return false;
  const url = source.url?.trim() || null;
  if (entry.source === "pr-review") {
    return cause.channel === "pull_request" && PR_KINDS.has(source.kind) && url !== null && entry.url?.trim() === url;
  }
  if (entry.source === "ticket") {
    return cause.channel !== "pull_request" && source.kind === "comment" && (url === null || url === ticketUrl?.trim());
  }
  return false;
}

/** The entry an attempt's cause names, or null — what ?causeOf={runId} scrolls to. */
export function causeEntryOf(
  entries: TicketConversationEntry[], cause: AttemptCause | null | undefined, ticketUrl: string | null = null,
): TicketConversationEntry | null {
  return entries.find((e) => causeEntryMatch(e, cause, ticketUrl)) ?? null;
}
