"use client";

import type { ReactNode } from "react";
import { Markdown } from "@/components/ui/Markdown";
import type { DialogEntry } from "@/hooks/useSpecDialog";
import type { SpecDialogProposalPush } from "@/types/spec-dialog";
import { DialogProposalCard } from "./DialogProposalCard";

// 2026-09-15-cb3e: the conversation in order. What the agent sent is MARKDOWN and is
// rendered as such — the framework composes its lines for this channel and the design
// master writes ordinary markdown, so both read as formatting rather than as punctuation.
// What the operator typed is rendered as the plain text they typed.
// 2026-09-17-c7aed: a turn that proposed something carries a card, and a turn that was only
// the draft is the card alone.

export function DialogTranscript({
  entries,
  onInspect,
}: {
  entries: DialogEntry[];
  onInspect: (proposal: SpecDialogProposalPush) => void;
}) {
  if (entries.length === 0) {
    // Nobody arrives wanting an epic. They arrive wanting something to be true of the
    // system, and the spec is the TRANSITION to it — so the question comes first and the
    // shapes it can end in are a footnote, not the headline. What is offered is the shape
    // of a good opening, not invented examples: this page does not know the work, and a
    // plausible-sounding suggestion about somebody's own estate is worse than none.
    return (
      <div data-testid="dialog-transcript-empty" className="dsh-body text-body">
        <p className="dsh-h3 text-ink">What do you want to be true, and where?</p>
        <p className="mt-2">
          Say the outcome you are after, the part of the system it touches, and anything
          that has to stay true while getting there. You do not need to know the steps —
          working those out is what this conversation is for.
        </p>
        <p className="mt-2">
          The design partner reads the repositories on the right before it answers, so the
          first reply takes about a minute. It will ask when something is ambiguous.
        </p>
        <p className="mt-3 eyebrow-uppercase">Where it leads</p>
        <p className="mt-1">
          When you have converged, it proposes what to file — an answer and nothing filed,
          one bug, one phase, or an epic with its slices in the order they will be filed. You approve
          it or you keep talking. Filing is where this ends: the run starts when the tracker
          picks the ticket up.
        </p>
      </div>
    );
  }

  return (
    <div data-testid="dialog-transcript" className="flex flex-col gap-4">
      {entries.map((entry) => (
        <Turn key={entry.key} entry={entry} onInspect={onInspect} />
      ))}
    </div>
  );
}

function Turn({
  entry,
  onInspect,
}: {
  entry: DialogEntry;
  onInspect: (proposal: SpecDialogProposalPush) => void;
}) {
  const mine = entry.kind === "user";
  const said = entry.text.trim().length > 0;
  return (
    <DialogMessage who={mine ? "user" : "agent"} testId={said ? `dialog-turn-${entry.kind}` : "dialog-turn-card"}>
      {mine ? (
        <p className="dsh-body whitespace-pre-wrap text-ink">{entry.text}</p>
      ) : (
        said && <Markdown>{entry.text}</Markdown>
      )}
      {entry.proposal && <DialogProposalCard proposal={entry.proposal} onInspect={onInspect} />}
    </DialogMessage>
  );
}

/** One row of the exchange: who said it, and what. The working line uses it too. */
export function DialogMessage({
  who,
  testId,
  children,
}: {
  who: "user" | "agent";
  testId?: string;
  children: ReactNode;
}) {
  return (
    <div data-testid={testId} className="grid grid-cols-[24px_minmax(0,1fr)] gap-2.5">
      <div
        aria-hidden="true"
        className={
          who === "user"
            ? "mt-px grid size-6 place-items-center rounded-md bg-canvas-soft font-mono dsh-label text-body"
            : "mt-px grid size-6 place-items-center rounded-md bg-primary-deep font-mono dsh-label text-on-primary"
        }
      >
        {who === "user" ? "OP" : "AS"}
      </div>
      <div className="min-w-0">
        <span className="sr-only">{who === "user" ? "You:" : "agent-smith:"}</span>
        {children}
      </div>
    </div>
  );
}
