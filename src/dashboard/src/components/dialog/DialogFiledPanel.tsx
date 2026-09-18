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
      <h2 className="dsh-h3 mb-1 font-semibold text-ink">
        {filed.error === null ? "Filed" : "Filing failed"}
      </h2>
      <p className="mb-2 dsh-label text-body">
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
              {ticket.start && <Start start={ticket.start} reference={ticket.reference} />}
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

const STARTED_LABEL: Record<SpecDialogFiledStart["state"], string> = {
  Started: "started",
  NotStarted: "not started",
  Record: "record",
};

// The state first, then why — a person scanning the list reads the verdicts, and only stops on
// the one that did not start.
function Start({ start, reference }: { start: SpecDialogFiledStart; reference: string }) {
  const tone =
    start.state === "Started"
      ? "text-primary-deep"
      : start.state === "NotStarted"
        ? "text-ink"
        : "text-body";
  return (
    <div data-testid={`dialog-filed-start-${reference}`} className="dsh-label text-body">
      <span className={`font-semibold ${tone}`}>{STARTED_LABEL[start.state]}</span>
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
      <span className="font-mono dsh-label font-semibold">{name}</span>
      <span className="ml-2 dsh-body">{ticket.title}</span>
    </>
  );
  if (!/^https?:\/\//.test(ticket.reference)) return <div className="text-ink">{body}</div>;
  return (
    <a
      href={ticket.reference}
      target="_blank"
      rel="noreferrer"
      className="text-primary-deep underline hover:text-primary-pressed"
    >
      {body}
    </a>
  );
}
