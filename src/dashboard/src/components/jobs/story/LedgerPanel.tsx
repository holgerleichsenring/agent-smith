"use client";

import { SectionLabel } from "@/components/ui/SectionLabel";
import { cn } from "@/lib/utils";
import type { ProgressLedgerEntry } from "@/types/hub-events";

// p0344b: the Building beat's content — the run's PERSISTED p0341 progress
// ledger (what the agent planned to do, what it is doing, what it finished),
// served on the run detail. Renders only when the snapshot carries the ledger;
// old runs have none and the panel simply does not exist for them.

const MARK: Record<ProgressLedgerEntry["status"], { glyph: string; cls: string; label: string }> = {
  done: { glyph: "✓", cls: "text-emerald-600", label: "done" },
  in_progress: { glyph: "●", cls: "text-amber-500 animate-pulse", label: "in progress" },
  pending: { glyph: "○", cls: "text-stone-400", label: "pending" },
};

export function LedgerPanel({ entries }: { entries: ProgressLedgerEntry[] }) {
  return (
    <div data-testid="ledger-panel" className="card-content p-4">
      <SectionLabel>Building — progress ledger</SectionLabel>
      <ol className="mt-3 space-y-1.5" data-testid="ledger-rows">
        {entries.map((entry, i) => {
          const mark = MARK[entry.status];
          return (
            <li
              key={entry.id}
              data-testid={`ledger-row-${entry.id}`}
              data-status={entry.status}
              className="flex items-baseline gap-2.5"
            >
              <span className="w-6 flex-none text-right font-mono dsh-mono text-stone-400">
                {i + 1}
              </span>
              <span
                className={cn("flex-none font-mono dsh-mono", mark.cls)}
                aria-label={mark.label}
              >
                {mark.glyph}
              </span>
              <span
                className={cn(
                  "min-w-0 dsh-body",
                  entry.status === "done" ? "text-stone-500" : "text-stone-800",
                )}
              >
                {entry.activity}
              </span>
              {entry.target && (
                <code
                  data-testid={`ledger-row-${entry.id}-target`}
                  className="ml-auto flex-none rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-mono text-stone-500"
                >
                  {entry.target}
                </code>
              )}
            </li>
          );
        })}
      </ol>
    </div>
  );
}
