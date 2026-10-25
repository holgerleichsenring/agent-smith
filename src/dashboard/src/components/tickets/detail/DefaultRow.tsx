"use client";

import type { TicketDefault } from "@/types/tickets";
import { age } from "../ticketFormat";

// 2026-10-13-8762q: one default on the Defaults tab — where it was taken, what was asked and chosen, and for a
// decided one who decided it, when and with which note. 2026-10-25-6c1f: the memory-proposal pointer is gone;
// the defaults read stopped serving those rows in 2026-10-25-9324e.

export const IN_THE_CONVERSATION = "in the design conversation";

interface Props {
  entry: TicketDefault;
  /** Null: no checkbox (a decided row, or a caller known to lack runs.control). */
  selected: boolean | null;
  onToggle?: (id: number) => void;
}

export function DefaultRow({ entry, selected, onToggle }: Props) {
  const pending = entry.verdict === "pending";
  const held = pending && entry.holdsSeries;
  return (
    <div className={held ? "df-row held" : "df-row"} data-testid={`default-${entry.id}`}>
      {selected !== null ? (
        <input type="checkbox" checked={selected} onChange={() => onToggle?.(entry.id)}
          aria-label={`Select default #${entry.id}`} data-testid={`default-select-${entry.id}`} />
      ) : <span />}
      <div className="df-main">
        <div className="df-meta">
          <Origin entry={entry} />
          {entry.specId && <span>spec {entry.specId}</span>}
          {ago(entry.takenAt) && <span>{ago(entry.takenAt)}</span>}
          {held && <span className="df-held" data-testid="default-holds">holds the series</span>}
        </div>
        {entry.question && <div className="df-q">{entry.question}</div>}
        <Body entry={entry} />
        {!pending && <Verdict entry={entry} />}
      </div>
    </div>
  );
}

function ago(iso: string | null): string | null {
  const span = age(iso);
  return span === null ? null : span === "now" ? "just now" : `${span} ago`;
}

function Origin({ entry }: { entry: TicketDefault }) {
  if (entry.conversation) return <span data-testid="default-origin">{IN_THE_CONVERSATION}</span>;
  if (entry.attempt !== null) return <span data-testid="default-origin">attempt {entry.attempt}</span>;
  return <span className="mono" data-testid="default-origin">run {entry.runId}</span>;
}

function Body({ entry }: { entry: TicketDefault }) {
  if (entry.chosen === null && entry.why === null) return null;
  const alternatives = entry.alternatives ?? [];
  return (
    <dl className="df-body" data-testid="default-body">
      {entry.chosen !== null && <><dt>chosen</dt><dd>{entry.chosen}</dd></>}
      {alternatives.length > 0 && <><dt>instead of</dt><dd>{alternatives.join(" · ")}</dd></>}
      {entry.why !== null && <><dt>why</dt><dd>{entry.why}</dd></>}
    </dl>
  );
}

function Verdict({ entry }: { entry: TicketDefault }) {
  const when = ago(entry.verdictAt);
  return (
    <div className="df-verdict" data-testid="default-verdict">
      <span className={entry.verdict === "ratified" ? "pill ok" : "pill bad"}>{entry.verdict}</span>
      {entry.verdictBy && <span>by {entry.verdictBy}</span>}
      {when && <span>{when}</span>}
      {entry.verdictNote && <span className="df-note">“{entry.verdictNote}”</span>}
    </div>
  );
}
