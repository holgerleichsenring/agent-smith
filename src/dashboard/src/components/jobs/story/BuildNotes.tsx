"use client";

import { useMemo } from "react";
import { useRunDecisions } from "@/hooks/useRunDecisions";
import type { RunDecisionRow } from "@/lib/runStepsApi";
import {
  EventType,
  type RunEvent,
  type SubAgentFileWrittenEvent,
} from "@/types/hub-events";

// p0343c: the Building beat's second card — the mock's "Latest decisions &
// changes" .note-row list, bound to REAL data: logged decisions (◆) and
// sub-agent file writes (✎). Renders nothing when neither exists — no
// fabricated activity.
//
// p0388b: the decisions come from the durable RunDecision projection, not from
// the client's live event buffer. A decision logged in the first minute of a
// four-hour run is still here; before, it survived only until the buffer rolled.
// File writes stay a live-window read — they are a "what just happened" signal.

const MAX_ROWS = 6;

interface Note {
  key: string;
  icon: string;
  body: React.ReactNode;
  meta: string;
}

export function BuildNotes({ runId, events }: { runId: string; events: RunEvent[] }) {
  const decisions = useRunDecisions(runId, events.length);
  const notes = useMemo(() => deriveNotes(decisions, events), [decisions, events]);
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

function deriveNotes(decisions: RunDecisionRow[], events: RunEvent[]): Note[] {
  const notes: Note[] = decisions.slice(0, MAX_ROWS).map((d, i) => ({
    key: `decision-${i}`,
    icon: "◆",
    body: (
      <>
        {d.name}
        {d.reason ? <> — {d.reason}</> : null}
      </>
    ),
    meta: `decision · ${timeOf(d.recordedAt)}`,
  }));
  for (let i = events.length - 1; i >= 0 && notes.length < MAX_ROWS; i--) {
    const e = events[i];
    if (e.type === EventType.SubAgentFileWritten) {
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
