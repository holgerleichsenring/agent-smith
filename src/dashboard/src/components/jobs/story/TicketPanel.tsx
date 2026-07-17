"use client";

import { useMemo } from "react";
import {
  EventType,
  type RunEvent,
  type RunSnapshot,
  type TicketFetchedEvent,
} from "@/types/hub-events";
import { Markdown } from "@/components/ui/Markdown";

// p0343c: the Ticket beat's stage — the run-viewer.html ticket reading mode
// (.ticket-layout: document article + details/attachments/acceptance aside),
// bound to the REAL TicketFetched event from the run's stream. Attachments
// render as a COUNT only (the event carries no thumbnails/files — nothing is
// faked). The acceptance box shows the persisted ratified criteria when the
// snapshot carries them.

interface TicketPanelProps {
  snapshot: RunSnapshot | null;
  events: RunEvent[];
}

export function TicketPanel({ snapshot, events }: TicketPanelProps) {
  const ticket = useMemo(() => latestTicketFetched(events), [events]);
  const criteria = snapshot?.acceptance?.criteria ?? [];

  if (!ticket) {
    return (
      <section className="card" data-testid="ticket-panel">
        <div className="card-b">
          <p className="hint" data-testid="ticket-panel-empty">
            The ticket body is not on this run’s event stream
            {snapshot?.ticketId ? (
              <>
                {" "}— only its reference <span className="mono">{snapshot.ticketId}</span>
                {snapshot.ticketTitle ? <> · {snapshot.ticketTitle}</> : null} is known.
              </>
            ) : (
              "."
            )}
          </p>
        </div>
      </section>
    );
  }

  return (
    <div className="ticket-layout" data-testid="ticket-panel">
      <article className="card">
        <div className="card-b">
          <div className="ticket-doc" data-testid="ticket-panel-description">
            <Markdown>{ticket.description}</Markdown>
          </div>
        </div>
      </article>
      <aside className="ticket-aside">
        <section className="card">
          <div className="card-b">
            <div className="asd-h">Details</div>
            <div className="aside-fields">
              <div className="af">
                <span className="k">Ticket</span>
                <span className="v mono" data-testid="ticket-panel-id">{ticket.ticketId}</span>
              </div>
              <div className="af">
                <span className="k">State</span>
                <span className="v">{ticket.state}</span>
              </div>
              <div className="af">
                <span className="k">Source</span>
                <span className="v">{ticket.source}</span>
              </div>
              {ticket.labels.length > 0 && (
                <div className="af">
                  <span className="k">Labels</span>
                  <span className="v">{ticket.labels.join(", ")}</span>
                </div>
              )}
            </div>
          </div>
        </section>
        <section className="card">
          <div className="card-b">
            <div className="asd-h">Attachments</div>
            {/* Count only — the event does not carry the files themselves. */}
            <div className="att-grid" data-testid="ticket-panel-attachments">
              {ticket.attachmentCount > 0 ? (
                <div className="att-file">
                  <div className="fi">▤</div>
                  <div>
                    <div className="fn">
                      {ticket.attachmentCount}{" "}
                      {ticket.attachmentCount === 1 ? "attachment" : "attachments"}
                    </div>
                    <div className="fs">on the ticket — open it in the tracker to view</div>
                  </div>
                </div>
              ) : (
                <span className="fs" style={{ fontSize: "12px", color: "var(--ink-3)" }}>
                  none
                </span>
              )}
            </div>
          </div>
        </section>
        {criteria.length > 0 && (
          <section className="accept-box" data-testid="ticket-panel-acceptance">
            <div className="ab-h">
              <svg viewBox="0 0 16 16" width="13" height="13" fill="none" aria-hidden="true">
                <path
                  d="M3.5 8.5l3 3 6-7"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                />
              </svg>
              Acceptance — ratified before any code
            </div>
            <ul>
              {criteria.map((c, i) => (
                <li key={i}>{c.text}</li>
              ))}
            </ul>
          </section>
        )}
      </aside>
    </div>
  );
}

function latestTicketFetched(events: RunEvent[]): TicketFetchedEvent | null {
  let latest: TicketFetchedEvent | null = null;
  for (const e of events) {
    if (e.type === EventType.TicketFetched) latest = e as TicketFetchedEvent;
  }
  return latest;
}
