"use client";

import { Markdown } from "@/components/ui/Markdown";
import type { DialogEntry } from "@/hooks/useSpecDialog";

// 2026-09-15-cb3e: the conversation in order. What the agent sent is MARKDOWN and is
// rendered as such — the framework composes its lines for this channel and the design
// master writes ordinary markdown, so both read as formatting rather than as punctuation.
// What the operator typed is rendered as the plain text they typed.

export function DialogTranscript({ entries }: { entries: DialogEntry[] }) {
  if (entries.length === 0) {
    return (
      <p data-testid="dialog-transcript-empty" className="text-sm text-[var(--color-ink-mid)]">
        Nothing said yet. Describe what you want built, and the design partner answers here.
      </p>
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
