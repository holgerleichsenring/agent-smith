"use client";

import { cn } from "@/lib/utils";
import type { ProgressLedgerEntry } from "@/types/hub-events";

// p0344b: the Building beat's content — the run's PERSISTED p0341 progress
// ledger, served on the run detail. Renders only when the snapshot carries the
// ledger; old runs have none and the panel simply does not exist for them.
// p0343c (pixel identity): emits the run-viewer.html ledger DOM verbatim —
// .li rows (done/run/pending) with the .box check square, .act activity,
// .note target and .tag state, plus the .ledger-foot progress bar.

const CHECK = (
  <svg viewBox="0 0 16 16" fill="none" aria-hidden="true">
    <path
      d="M3.5 8.5l3 3 6-7"
      stroke="#fff"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

const LI_CLASS: Record<ProgressLedgerEntry["status"], string> = {
  done: "done",
  in_progress: "run",
  pending: "pending",
};

const TAG: Record<ProgressLedgerEntry["status"], string> = {
  done: "done",
  in_progress: "now",
  pending: "next",
};

export function LedgerPanel({ entries }: { entries: ProgressLedgerEntry[] }) {
  const done = entries.filter((e) => e.status === "done").length;
  const now = entries.filter((e) => e.status === "in_progress").length;
  const toGo = entries.length - done - now;
  const donePct = entries.length > 0 ? Math.round((done / entries.length) * 100) : 0;
  const nowPct = entries.length > 0 ? Math.round((now / entries.length) * 100) : 0;

  return (
    <div data-testid="ledger-panel">
      <div data-testid="ledger-rows">
        {entries.map((entry) => (
          <div
            key={entry.id}
            className={cn("li", LI_CLASS[entry.status])}
            data-testid={`ledger-row-${entry.id}`}
            data-status={entry.status}
          >
            <div className="box">{entry.status === "done" ? CHECK : null}</div>
            <div>
              <div className="act">{entry.activity}</div>
              {entry.target && (
                <div className="note" data-testid={`ledger-row-${entry.id}-target`}>
                  {entry.target}
                </div>
              )}
            </div>
            <div className="tag">{TAG[entry.status]}</div>
          </div>
        ))}
      </div>
      <div className="ledger-foot">
        <div className="bar">
          <i className="d" style={{ width: `${donePct}%` }} />
          <i className="r" style={{ width: `${nowPct}%` }} />
        </div>
        <span className="num" data-testid="ledger-foot-caption">
          {done} done · {now} now · {toGo} to go
        </span>
      </div>
    </div>
  );
}
