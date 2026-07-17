"use client";

import { useMemo } from "react";
import {
  EventType,
  type DecisionLoggedEvent,
  type RunEvent,
  type SubAgentFileWrittenEvent,
} from "@/types/hub-events";

// p0343c: the Building beat's second card — the mock's "Latest decisions &
// changes" .note-row list, bound to REAL stream events: DecisionLogged (◆) and
// SubAgentFileWritten (✎). Renders nothing when the stream carries neither —
// no fabricated activity.

const MAX_ROWS = 6;

interface Note {
  key: string;
  icon: string;
  body: React.ReactNode;
  meta: string;
}

export function BuildNotes({ events }: { events: RunEvent[] }) {
  const notes = useMemo(() => deriveNotes(events), [events]);
  if (notes.length === 0) return null;
  return (
    <section className="card" data-testid="build-notes">
      <div className="card-h">
        <h3>Latest decisions &amp; changes</h3>
        <span className="badge neu">live</span>
      </div>
      <div className="card-b">
        {notes.map((note) => (
          <div className="note-row" key={note.key}>
            <div className="ic">{note.icon}</div>
            <div className="body">
              {note.body}
              <div className="w">{note.meta}</div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

function timeOf(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleTimeString();
}

function deriveNotes(events: RunEvent[]): Note[] {
  const notes: Note[] = [];
  for (let i = events.length - 1; i >= 0 && notes.length < MAX_ROWS; i--) {
    const e = events[i];
    if (e.type === EventType.DecisionLogged) {
      const d = e as DecisionLoggedEvent;
      notes.push({
        key: `decision-${i}`,
        icon: "◆",
        body: (
          <>
            {d.chose}
            {d.reason ? <> — {d.reason}</> : null}
          </>
        ),
        meta: `decision · ${d.category} · ${timeOf(d.timestamp)}`,
      });
    } else if (e.type === EventType.SubAgentFileWritten) {
      const f = e as SubAgentFileWrittenEvent;
      notes.push({
        key: `file-${i}`,
        icon: "✎",
        body: <span className="file">{f.path}</span>,
        meta: `write · ${f.bytes} bytes · ${timeOf(f.timestamp)}`,
      });
    }
  }
  return notes;
}
