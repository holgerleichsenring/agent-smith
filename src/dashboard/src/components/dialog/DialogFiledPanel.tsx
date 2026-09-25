"use client";

import type { FiledWork, SpecDialogFilingPush } from "@/types/spec-dialog";
import { DialogFiledTicket, type NamedTicket } from "./DialogFiledTicket";

// 2026-09-15-6d9c: WHAT WAS FILED — the tickets that now exist, by reference and title. The
// report behind it is honest about a partial failure, and so is this: the tickets that WERE
// created are listed above the error that stopped the rest, because they exist either way.
// 2026-09-17-042ea: a note says what went wrong without unfiling anything, such as a child the
// tracker would not link to its parent; it sits under the tickets it is about.
// 2026-09-17-042eg: each ticket says what it BECAME — started, not started with the reason
// nothing will pick it up, or a record, which is not work. A filing written before that phase
// carries no start state at all and renders exactly as it did, because an absent state is
// unknown and a panel that guessed would be making the claim the state exists to stop.

// 2026-09-17-042ej: and what became of it — under each ticket, the runs that took it up and,
// per run, a row per PHASE with its pull requests and what the review still finds. The filing
// push arrives first and alone; the work is a read of its own and appears beneath it.

// 2026-09-17-042ef: the head, the key and the tracker link are the studio's entity name, field
// value and this page's own link.

// 2026-09-25-c4a6: A CONVERSATION BOUND TO A TICKET FILED NOTHING, so there is no push to draw
// from and the READ is the whole content of the pane. The rows are the same rows — a ticket, what
// it became, and the runs that worked it — and only the sentence above them differs, because
// "these tickets now exist" is a claim about an act this conversation did not perform.

export function DialogFiledPanel({
  filed,
  work,
}: {
  filed: SpecDialogFilingPush | null;
  work: FiledWork | null;
}) {
  const partial = filed !== null && filed.error !== null && filed.filed.length > 0;
  // A push from a server older than filing notes carries none.
  const notes = filed?.notes ?? [];
  const tickets: NamedTicket[] = filed?.filed ?? work?.tickets ?? [];
  return (
    <div data-testid="dialog-filed">
      {/* 2026-09-17-042ef: the heading said "Filed" directly under a tab reading Filed and an
          eyebrow reading filed. The tab names the pane; this line, which the heading never
          said, is what is left. */}
      <p
        className={
          filed === null || filed.error === null
            ? "ec-sub mb-2"
            : "dsh-body mb-2 font-semibold text-ink"
        }
      >
        {filed === null
          ? "This conversation's ticket, and the work on it."
          : filed.error === null
            ? "These tickets now exist."
            : partial
              ? "These tickets were created before it stopped — they exist."
              : "Nothing was created."}
      </p>
      {tickets.length > 0 && (
        <ul className="dsh-body flex flex-col gap-2">
          {tickets.map((ticket) => (
            <li key={ticket.reference} data-testid={`dialog-filed-${ticket.reference}`}>
              <DialogFiledTicket ticket={ticket} work={work} />
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
      {filed?.error != null && (
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
