import type { SpecDialogGraphTicket } from "@/types/spec-dialog";

// 2026-10-14-dbcek: what a ticket of a proposed list waits for, worded as the chat confirmation words it. A sibling is
// named by its key; an existing ticket (`ticket:<id>`) as #<id> when the id is numeric and as written otherwise (a Jira
// key) — the proposal carries no tracker type.

import type { T } from "@/lib/i18n/translate";

const EXISTING = "ticket:";

/** A requires entry as a person reads it. */
export function requiredName(requirement: string): string {
  if (!requirement.startsWith(EXISTING)) return requirement;
  const raw = requirement.slice(EXISTING.length).trim();
  const id = raw.startsWith("#") ? raw.slice(1).trim() : raw;
  return /^\d+$/.test(id) ? `#${id}` : id;
}

/** "nothing to wait for", or "waits for a, #41". */
export function waitsFor(t: T, ticket: SpecDialogGraphTicket): string {
  return ticket.requires.length === 0
    ? t("dialog.waits.nothing")
    : t("dialog.waits.for", { list: ticket.requires.map(requiredName).join(", ") });
}

/** The key a finding about the whole list carries — never a ticket key. */
export const LIST_KEY = "the list";
