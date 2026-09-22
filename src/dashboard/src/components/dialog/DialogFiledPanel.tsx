"use client";

import type {
  FiledWork,
  SpecDialogFiledStart,
  SpecDialogFiledTicket,
  SpecDialogFilingPush,
} from "@/types/spec-dialog";
import { DialogFiledRuns } from "./DialogFiledRuns";

// 2026-09-15-6d9c: WHAT WAS FILED — the tickets that now exist, by reference and title. The
// report behind it is honest about a partial failure, and so is this: the tickets that WERE
// created are listed above the error that stopped the rest, because they exist either way.
// 2026-09-17-042ea: a note says what went wrong without unfiling anything, such as a child the
// tracker would not link to its parent; it sits under the tickets it is about.
// 2026-09-17-042eg: each ticket says what it BECAME — started, not started with the reason
// nothing will pick it up, or a record, which is not work. A filing written before that phase
// carries no start state at all and renders exactly as it did, because an absent state is
// unknown and a panel that guessed would be making the claim the state exists to stop.
// 2026-09-17-042em: a ticket is named by its KEY, with its title beside it and the tracker's page
// behind both — a column of web urls differing in their last few digits is the one part of them
// nobody reads. A filing written before this phase carries no key and reads by its reference, as
// it always did.

// 2026-09-17-042ej: and what became of it — under each ticket, the runs that took it up and,
// per run, a row per PHASE with its pull requests and what the review still finds. The filing
// push arrives first and alone; the work is a read of its own and appears beneath it.

// 2026-09-17-042ef: the head, the key and the tracker link are the studio's entity name, field
// value and this page's own link.

export function DialogFiledPanel({
  filed,
  work,
}: {
  filed: SpecDialogFilingPush;
  work: FiledWork | null;
}) {
  const partial = filed.error !== null && filed.filed.length > 0;
  // A push from a server older than filing notes carries none.
  const notes = filed.notes ?? [];
  return (
    <div data-testid="dialog-filed">
      {/* 2026-09-17-042ef: the heading said "Filed" directly under a tab reading Filed and an
          eyebrow reading filed. The tab names the pane; this line, which the heading never
          said, is what is left. */}
      <p className={filed.error === null ? "ec-sub mb-2" : "dsh-body mb-2 font-semibold text-ink"}>
        {filed.error === null
          ? "These tickets now exist."
          : partial
            ? "These tickets were created before it stopped — they exist."
            : "Nothing was created."}
      </p>
      {filed.filed.length > 0 && (
        <ul className="dsh-body flex flex-col gap-2">
          {filed.filed.map((ticket) => (
            <li key={ticket.reference} data-testid={`dialog-filed-${ticket.reference}`}>
              <Named ticket={ticket} />
              <Start work={work} ticket={ticket} />
              <Work work={work} reference={ticket.reference} />
            </li>
          ))}
        </ul>
      )}
      {notes.length > 0 && (
        <ul data-testid="dialog-filed-notes" className="mt-2 dsh-label flex flex-col gap-1 text-body">
          {notes.map((note) => (
            <li key={note}>{note}</li>
          ))}
        </ul>
      )}
      {filed.error !== null && (
        <p
          data-testid="dialog-filed-error"
          className="mt-2 dsh-label whitespace-pre-wrap text-ink"
        >
          {filed.error}
        </p>
      )}
    </div>
  );
}

// The read answers for the same filing the push carries, so a ticket is matched by the one
// thing both name. A read that has not come back yet, or an older one, simply shows nothing.
function Work({ work, reference }: { work: FiledWork | null; reference: string }) {
  const row = work?.tickets.find((ticket) => ticket.reference === reference);
  if (!row) return null;
  return (
    <DialogFiledRuns runs={row.runs} handback={row.handback} reference={reference} />
  );
}

// 2026-09-22-9519: THE READ ANSWERS FIRST. The push is what filing said at filing time and it is
// never sent again, so a ticket the conversation withdrew afterwards would have gone on saying it
// was started until someone reloaded the page. The read is refetched on the nudge the withdrawal
// sends, and it carries the same start state — so it is the one that is rendered, with the push
// behind it for the moment before the first read comes back.
function startOf(
  work: FiledWork | null,
  ticket: SpecDialogFiledTicket,
): SpecDialogFiledStart | null {
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
};

// The state first, then why — a person scanning the list reads the verdicts, and only stops on
// the one that did not start. A filing written before start states carries none, and the line is
// not rendered at all: a panel that guessed would make the claim the state exists to stop.
function Start({ work, ticket }: { work: FiledWork | null; ticket: SpecDialogFiledTicket }) {
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
// text where it is not, because a bare reference is not a link and must not read as one.
function Named({ ticket }: { ticket: SpecDialogFiledTicket }) {
  const name = ticket.key ?? ticket.reference;
  const body = (
    <>
      <span className="fv">{name}</span>
      <span className="ml-2 dsh-body">{ticket.title}</span>
    </>
  );
  if (!/^https?:\/\//.test(ticket.reference)) return <div className="text-ink">{body}</div>;
  return (
    <a
      href={ticket.reference}
      target="_blank"
      rel="noreferrer"
      className="d-link"
    >
      {body}
    </a>
  );
}
