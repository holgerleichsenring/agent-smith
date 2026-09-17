"use client";

import { Markdown } from "@/components/ui/Markdown";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-09-15-cb3e: the conversation in order. What the agent sent is MARKDOWN and is
// rendered as such — the framework composes its lines for this channel and the design
// master writes ordinary markdown, so both read as formatting rather than as punctuation.
// What the operator typed is rendered as the plain text they typed.

export function DialogTranscript({ entries }: { entries: DialogEntry[] }) {
  if (entries.length === 0) {
    // Nobody arrives wanting an epic. They arrive wanting something to be true of the
    // system, and the spec is the TRANSITION to it — so the question comes first and the
    // shapes it can end in are a footnote, not the headline. What is offered is the shape
    // of a good opening, not invented examples: this page does not know the work, and a
    // plausible-sounding suggestion about somebody's own estate is worse than none.
    return (
      <div data-testid="dialog-transcript-empty" className="dsh-body text-[var(--color-ink-mid)]">
        <p className="dsh-h3">What do you want to be true, and where?</p>
        <p className="mt-2">
          Say the outcome you are after, the part of the system it touches, and anything
          that has to stay true while getting there. You do not need to know the steps —
          working those out is what this conversation is for.
        </p>
        <p className="mt-2">
          The design partner reads the repositories on the right before it answers, so the
          first reply takes about a minute. It will ask when something is ambiguous.
        </p>
        <p className="mt-3 dsh-label uppercase tracking-wide">Where it leads</p>
        <p className="mt-1">
          When you have converged, it proposes what to file — an answer and nothing filed,
          one bug, one phase, or an epic with its slices in the order they run. You approve
          it or you keep talking. Filing is where this ends: the run starts when the tracker
          picks the ticket up.
        </p>
      </div>
    );
  }

  return (
    <div data-testid="dialog-transcript" className="flex flex-col gap-3">
      {entries.map((entry) => (
        <Turn key={entry.key} entry={entry} />
      ))}
    </div>
  );
}

function Turn({ entry }: { entry: DialogEntry }) {
  const mine = entry.kind === "user";
  return (
    <div
      data-testid={`dialog-turn-${entry.kind}`}
      className={
        mine
          ? "self-end max-w-[85%] rounded border border-stone-200 bg-stone-50 px-3 py-2"
          : "max-w-[95%] rounded border border-stone-200 px-3 py-2"
      }
    >
      <div className="mb-1 dsh-label uppercase tracking-wide text-[var(--color-ink-mid)]">
        {mine ? "you" : "agent-smith"}
      </div>
      {mine ? (
        <p className="dsh-body whitespace-pre-wrap text-stone-700">{entry.text}</p>
      ) : (
        <Markdown>{entry.text}</Markdown>
      )}
    </div>
  );
}
