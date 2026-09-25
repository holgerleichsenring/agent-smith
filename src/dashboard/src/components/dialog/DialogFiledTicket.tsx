"use client";

import type { FiledWork, SpecDialogFiledStart } from "@/types/spec-dialog";
import { DialogFiledRuns } from "./DialogFiledRuns";

// 2026-09-25-c4a6: one ticket in the pane — its name, what it became, and the work on it. Lifted
// out of DialogFiledPanel because the rows no longer all come from a FILING: a conversation bound
// to a ticket it did not file has a row of its own, and both are drawn the same way.
// 2026-09-17-042em: a ticket is named by its KEY, with its title beside it and the tracker's page
// behind both — a column of web urls differing in their last few digits is the one part of them
// nobody reads. A row with no key reads by its reference, as a filing written before that phase
// always did, and a bound ticket reads by the tracker's own id.

/** What a filing's push row and the read's own row both carry — enough to name the ticket. */
export type NamedTicket = {
  reference: string;
  key?: string | null;
  title: string;
  start?: SpecDialogFiledStart | null;
};

export function DialogFiledTicket({
  ticket,
  work,
}: {
  ticket: NamedTicket;
  work: FiledWork | null;
}) {
  return (
    <>
      <Named ticket={ticket} />
      <Start work={work} ticket={ticket} />
      <Work work={work} reference={ticket.reference} />
    </>
  );
}

// The read answers for the same tickets the pane names, so a ticket is matched by the one thing
// both carry. A read that has not come back yet, or an older one, simply shows nothing.
function Work({ work, reference }: { work: FiledWork | null; reference: string }) {
  const row = work?.tickets.find((ticket) => ticket.reference === reference);
  if (!row) return null;
  return <DialogFiledRuns runs={row.runs} handback={row.handback} reference={reference} />;
}

// 2026-09-22-9519: THE READ ANSWERS FIRST. The push is what filing said at filing time and it is
// never sent again, so a ticket the conversation withdrew afterwards would have gone on saying it
// was started until someone reloaded the page. The read is refetched on the nudge the withdrawal
// sends, and it carries the same start state — so it is the one that is rendered, with the push
// behind it for the moment before the first read comes back.
function startOf(work: FiledWork | null, ticket: NamedTicket): SpecDialogFiledStart | null {
  const row = work?.tickets.find((read) => read.reference === ticket.reference);
  return row?.start ?? ticket.start ?? null;
}

// EXHAUSTIVE over the union on purpose: a state added to the server and not to this map fails the
// build here, which is the only place that can notice it before an operator does.
const STARTED_LABEL: Record<NonNullable<SpecDialogFiledStart["state"]>, string> = {
  Started: "started",
  NotStarted: "not started",
  Record: "record",
  Withdrawn: "withdrawn",
  NotFiled: "not filed here",
};

// The state first, then why — a person scanning the list reads the verdicts, and only stops on
// the one that did not start. A filing written before start states carries none, and the line is
// not rendered at all: a panel that guessed would make the claim the state exists to stop.
function Start({ work, ticket }: { work: FiledWork | null; ticket: NamedTicket }) {
  const start = startOf(work, ticket);
  if (!start) return null;
  const tone =
    start.state === "Started"
      ? "text-primary-deep"
      : start.state === "NotStarted"
        ? "text-ink"
        : "text-body";
  return (
    <div data-testid={`dialog-filed-start-${ticket.reference}`} className="dsh-label text-body">
      <span className={`font-semibold ${tone}`}>
        {start.state ? STARTED_LABEL[start.state] : "unknown"}
      </span>
      {" — "}
      {start.reason}
    </div>
  );
}

// The key and the title, linked to the tracker wherever the reference is a web URL — and plain
// text where it is not, because a bare reference is not a link and must not read as one. A bound
// ticket's reference is the tracker's own id, which is exactly that case: the domain ticket
// carries no url, so there is no page to send a reader to.
function Named({ ticket }: { ticket: NamedTicket }) {
  const name = ticket.key ?? ticket.reference;
  const body = (
    <>
      <span className="fv">{name}</span>
      <span className="ml-2 dsh-body">{ticket.title}</span>
    </>
  );
  if (!/^https?:\/\//.test(ticket.reference)) return <div className="text-ink">{body}</div>;
  return (
    <a href={ticket.reference} target="_blank" rel="noreferrer" className="d-link">
      {body}
    </a>
  );
}
