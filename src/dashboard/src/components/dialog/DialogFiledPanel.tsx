"use client";

import type { SpecDialogFilingPush } from "@/types/spec-dialog";

// 2026-09-15-6d9c: WHAT WAS FILED — the tickets that now exist, by reference and title. The
// report behind it is honest about a partial failure, and so is this: the tickets that WERE
// created are listed above the error that stopped the rest, because they exist either way.
// 2026-09-17-042ea: a note says what went wrong without unfiling anything, such as a child the
// tracker would not link to its parent; it sits under the tickets it is about.

export function DialogFiledPanel({ filed }: { filed: SpecDialogFilingPush }) {
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
              <Reference reference={ticket.reference} />
              <div className="text-ink">{ticket.title}</div>
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

// A reference is a web URL wherever the tracker gives one, and a bare key where it does not.
function Reference({ reference }: { reference: string }) {
  if (!/^https?:\/\//.test(reference)) {
    return <div className="font-mono dsh-label font-semibold text-ink">{reference}</div>;
  }
  return (
    <a
      href={reference}
      target="_blank"
      rel="noreferrer"
      className="font-mono dsh-label font-semibold text-primary-deep underline hover:text-primary-pressed"
    >
      {reference}
    </a>
  );
}
