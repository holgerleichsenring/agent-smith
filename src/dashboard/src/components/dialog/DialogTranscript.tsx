"use client";

import type { ReactNode } from "react";
import { Markdown } from "@/components/ui/Markdown";
import { specDialogImageUrl } from "@/lib/specDialogApi";
import { isDecision, type DialogEntry } from "@/hooks/useSpecDialog";
import type { SpecDialogImage, SpecDialogProposalPush } from "@/types/spec-dialog";
import { DialogProposalCard } from "./DialogProposalCard";

// 2026-09-15-cb3e: the conversation in order. What the agent sent is MARKDOWN and is
// rendered as such — the framework composes its lines for this channel and the design
// master writes ordinary markdown, so both read as formatting rather than as punctuation.
// What the operator typed is rendered as the plain text they typed.
// 2026-09-17-c7aed: a turn that proposed something carries a card, and a turn that was only
// the draft is the card alone.
// 2026-09-17-042ej: the closing line of the empty state says where the conversation goes after
// filing, because it no longer stops there: the filed tab follows the run.
// 2026-09-17-042el: an approve or reject is shown as the decision it was, not as the word — as
// pending until a read confirms the server stored it. A decision this page cannot name is shown as
// the message it was.
// 2026-09-17-042ef: the eyebrows are the studio's field label and the speaker's initials are a
// mark of this page's own — the studio's card icon leads a card, this leads a line.
// 2026-09-20-3af8: an image the operator attached is one of their own lines, shown where they
// attached it. The bytes come from a route of their own, so the transcript read stays small.

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
        <p className="fl mt-3">Where it leads</p>
        <p className="mt-1">
          When you have converged, it proposes what to file — an answer and nothing filed,
          one bug, one phase, or an epic with its slices in the order they will be filed. You
          approve it or you keep talking. Filing is not where this ends: the conversation then
          follows the work it filed — which phase the run is on, what it opened, what the
          review still finds, and anything handed back for you to settle here.
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
  if (entry.kind === "decision" && isDecision(entry.decision)) return <Decision entry={entry} />;
  if (entry.kind === "image" && entry.image) return <Attached image={entry.image} />;
  const mine = entry.kind !== "agent";
  const said = entry.text.trim().length > 0;
  return (
    <DialogMessage who={mine ? "user" : "agent"} testId={said ? `dialog-turn-${mine ? "user" : "agent"}` : "dialog-turn-card"}>
      {mine ? (
        <p className="dsh-body whitespace-pre-wrap text-ink">{entry.text}</p>
      ) : (
        said && <Markdown>{entry.text}</Markdown>
      )}
      {entry.proposal && <DialogProposalCard proposal={entry.proposal} onInspect={onInspect} />}
    </DialogMessage>
  );
}

function Attached({ image }: { image: SpecDialogImage }) {
  return (
    <DialogMessage who="user" testId="dialog-turn-image">
      <img
        data-testid={`dialog-image-${image.id}`}
        src={specDialogImageUrl(image.id)}
        alt="Attached by you"
        className="max-h-64 max-w-full"
      />
    </DialogMessage>
  );
}

function Decision({ entry }: { entry: DialogEntry }) {
  const approved = entry.decision === "approved";
  if (entry.pending)
    return (
      <DialogMessage who="user" testId="dialog-turn-decision">
        <p data-decision={entry.decision} data-pending="true" className="dsh-body text-body">
          <span className="fl">Sent</span>{" "}
          {approved ? "Approve — waiting for it to be recorded" : "Reject — waiting for it to be recorded"}
        </p>
      </DialogMessage>
    );
  return (
    <DialogMessage who="user" testId="dialog-turn-decision">
      <p data-decision={entry.decision} className="dsh-body font-semibold text-ink">
        {approved ? "Approved — file the proposal" : "Rejected — nothing is filed"}
      </p>
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
        className={who === "user" ? "d-who mt-px" : "d-who agent mt-px"}
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
