"use client";

import type { SpecDialogFilingPush } from "@/types/spec-dialog";

// 2026-09-15-6d9c: WHAT WAS FILED — the tickets that now exist, by reference and title. The
// report behind it is honest about a partial failure, and so is this: the tickets that WERE
// created are listed above the error that stopped the rest, because they exist either way.

export function DialogFiledPanel({ filed }: { filed: SpecDialogFilingPush }) {
  const partial = filed.error !== null && filed.filed.length > 0;
  return (
    <aside data-testid="dialog-filed" className="rounded border border-stone-200 p-3">
      <h2 className="dsh-h3 mb-1 font-semibold text-stone-800">
        {filed.error === null ? "Filed" : "Filing failed"}
      </h2>
      <p className="mb-2 text-xs text-[var(--color-ink-mid)]">
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
              <div className="text-stone-700">{ticket.title}</div>
            </li>
          ))}
        </ul>
      )}
      {filed.error !== null && (
        <p
          data-testid="dialog-filed-error"
          className="mt-2 dsh-label whitespace-pre-wrap text-stone-700"
        >
          {filed.error}
        </p>
      )}
    </aside>
  );
}

// A reference is a web URL wherever the tracker gives one, and a bare key where it does not.
function Reference({ reference }: { reference: string }) {
  if (!/^https?:\/\//.test(reference)) {
    return <div className="dsh-label font-semibold text-stone-800">{reference}</div>;
  }
  return (
    <a
      href={reference}
      target="_blank"
      rel="noreferrer"
      className="dsh-label font-semibold text-emerald-700 underline hover:text-emerald-800"
    >
      {reference}
    </a>
  );
}
