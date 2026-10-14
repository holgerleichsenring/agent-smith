"use client";

import { useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { TrackerText } from "@/components/ui/TrackerText";
import { useTicketText } from "@/lib/tickets/TicketPageStore";
import type { TicketText, TicketTextReason } from "@/types/tickets";
import { clock } from "./flowWords";

// The ticket itself at the top of the Summary tab: its type, its state now, labels and assignee, the
// description as its tracker wrote it, and the tracker's own acceptance criteria — labelled apart from the
// Acceptance tab, which lists the specs' done criteria. A snapshot says which attempt read it and when.

const REASON_WORDS: Readonly<Record<TicketTextReason, string>> = {
  "not-found": "the ticket was not found there, or this connection may not see it",
  unauthorized: "the tracker refused this connection's credentials",
  unreachable: "the tracker did not answer",
  "project-not-configured": "its project is no longer configured",
};

export function TicketTextSection() {
  const read = useTicketText();
  const text = read.data;
  if (!text) {
    if (!read.failed) return null;
    return <section className="tt" data-testid="ticket-text"><div className="tt-note">The ticket could not be read.</div></section>;
  }
  return (
    <section className="tt" data-testid="ticket-text">
      <div className="tt-h">
        <span className="k">The ticket</span>
        <Chips text={text} />
      </div>
      {text.source === "unavailable" ? (
        <div className="tt-note" data-testid="ticket-text-unavailable">
          The tracker could not be read: {text.reason ? REASON_WORDS[text.reason] : REASON_WORDS.unreachable}.
        </div>
      ) : (
        <Body text={text} />
      )}
    </section>
  );
}

function Chips({ text }: { text: TicketText }) {
  return (
    <span className="tt-chips">
      {text.workItemType && <span className="tchip" data-testid="ticket-text-type">{text.workItemType}</span>}
      {text.state && <span className="tchip" data-testid="ticket-text-state">now: {text.state}</span>}
      {(text.labels ?? []).map((label) => <span key={label} className="tchip label">{label}</span>)}
      {text.assignee && <span className="tt-who" data-testid="ticket-text-assignee">assigned to {text.assignee}</span>}
    </span>
  );
}

function Body({ text }: { text: TicketText }) {
  return (
    <>
      {text.source === "snapshot" && (
        <div className="tt-note" data-testid="ticket-text-snapshot">
          as attempt {text.attempt} read it, {clock(text.at)} — the tracker could not be read
        </div>
      )}
      {text.description
        ? <Collapsed source={text.description}><TrackerText text={text.description} format={text.descriptionFormat} /></Collapsed>
        : <div className="tt-note">No description.</div>}
      {text.acceptanceCriteria && (
        <div className="tt-ac" data-testid="ticket-text-criteria">
          <span className="k">Acceptance criteria (from the ticket)</span>
          <div className="tt-body"><TrackerText text={text.acceptanceCriteria} format={text.criteriaFormat} /></div>
        </div>
      )}
    </>
  );
}

/** Twelve lines, and "Show all" when the text runs longer. */
function Collapsed({ source, children }: { source: string; children: ReactNode }) {
  const box = useRef<HTMLDivElement>(null);
  const [open, setOpen] = useState(false);
  const [overflows, setOverflows] = useState(false);
  useLayoutEffect(() => {
    const el = box.current;
    if (el) setOverflows(el.scrollHeight > el.clientHeight + 1);
  }, [source]);
  return (
    <>
      <div ref={box} className={open ? "tt-body" : "tt-body clamp"} data-testid="ticket-text-description">{children}</div>
      {overflows && !open && (
        <button type="button" className="tt-more" onClick={() => setOpen(true)}>Show all</button>
      )}
    </>
  );
}
