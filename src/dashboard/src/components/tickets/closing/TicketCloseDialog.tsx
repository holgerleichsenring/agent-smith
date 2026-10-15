"use client";

import { useEffect, useRef, useState } from "react";
import { closeTicket, type TicketCloseAnswer } from "@/lib/ticketCloseApi";

// 2026-10-15-114fd: ticket-view.html's Close dialog — an optional note and two host options, both unchecked: nothing
// chosen hides the ticket only. The pull-request option is offered only while one is open. A refusal is the server's
// sentence as is; a close re-reads at once and, when a host was asked, reports each pull request and the tracker.

interface Props {
  project: string;
  ticketId: string;
  /** The ticket's name as a person reads it — "#58", "PROJ-41". */
  label: string;
  /** "GitHub", "Jira", … */
  tracker: string;
  openPullRequests: number;
  onClose: () => void;
  onClosed: () => void;
}

type State = { kind: "editing" } | { kind: "sending" } | { kind: "said"; text: string } | { kind: "closed"; answer: TicketCloseAnswer };

const REASON_WORDS: Record<string, string> = { "already-closed": "already closed" };

export function TicketCloseDialog({ project, ticketId, label, tracker, openPullRequests, onClose, onClosed }: Props) {
  const [note, setNote] = useState("");
  const [prs, setPrs] = useState(false);
  const [onTracker, setOnTracker] = useState(false);
  const [state, setState] = useState<State>({ kind: "editing" });
  const field = useRef<HTMLTextAreaElement>(null);
  useEffect(() => field.current?.focus(), []);

  const send = async () => {
    setState({ kind: "sending" });
    try {
      const sent = await closeTicket(project, ticketId, { note: note.trim() || null, closePullRequests: prs, closeOnTracker: onTracker });
      if (sent.kind === "said") return setState({ kind: "said", text: sent.text });
      onClosed();
      if (sent.answer.pullRequests.length === 0 && !sent.answer.tracker) onClose();
      else setState({ kind: "closed", answer: sent.answer });
    } catch (thrown) {
      setState({ kind: "said", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
  };

  return (
    <div className="dbh-back" onClick={(e) => e.target === e.currentTarget && onClose()} data-testid="ticket-close-backdrop">
      <div className="dbh" role="dialog" aria-modal="true" aria-labelledby={`close-${ticketId}`} data-testid="ticket-close-dialog"
        onKeyDown={(e) => e.key === "Escape" && onClose()}>
        <h3 id={`close-${ticketId}`}>Close {label}</h3>
        {state.kind === "closed" ? (
          <Outcomes answer={state.answer} tracker={tracker} onDone={onClose} />
        ) : (
          <>
            <div className="dbh-sub">It moves to Closed. A later attempt, a person&apos;s entry on its pull request, or a merge brings it back.</div>
            <textarea ref={field} value={note} onChange={(e) => setNote(e.target.value)} aria-label="Note"
              placeholder="Why are you closing it? (optional)" data-testid="ticket-close-note" />
            {openPullRequests > 0 && (
              <label className="dbh-opt"><input type="checkbox" checked={prs} onChange={(e) => setPrs(e.target.checked)}
                data-testid="ticket-close-prs" /> Also close its open pull requests ({openPullRequests})</label>
            )}
            <label className="dbh-opt"><input type="checkbox" checked={onTracker} onChange={(e) => setOnTracker(e.target.checked)}
              data-testid="ticket-close-tracker" /> Also close {label} on {tracker}</label>
            {state.kind === "said" && <div className="dbh-said" data-testid="ticket-close-said">{state.text}</div>}
            <div className="dbh-acts">
              <button type="button" className="btn" onClick={onClose}>Cancel</button>
              <button type="button" className="btn primary" onClick={() => void send()} disabled={state.kind === "sending"}
                data-testid="ticket-close-send">Close ticket</button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function Outcomes({ answer, tracker, onDone }: { answer: TicketCloseAnswer; tracker: string; onDone: () => void }) {
  const lines = [
    ...answer.pullRequests.map((pr) => ({ key: pr.url, name: `PR #${pr.url.split("/").pop()}`, ...pr })),
    ...(answer.tracker ? [{ key: "tracker", name: `${tracker} ticket`, ...answer.tracker }] : []),
  ];
  return (
    <>
      <div className="dbh-sub">Closed. What happened on the hosts:</div>
      <div className="dbh-outs" data-testid="ticket-close-outcomes">
        {lines.map((line) => (
          <div key={line.key} className={line.closed ? "dbh-out closed" : "dbh-out kept"}>
            {line.name} — {outcomeWords(line.closed, line.reason)}
          </div>
        ))}
      </div>
      <div className="dbh-acts">
        <button type="button" className="btn primary" onClick={onDone} data-testid="ticket-close-done">Done</button>
      </div>
    </>
  );
}

function outcomeWords(closed: boolean, reason: string | null): string {
  if (closed) return reason ? REASON_WORDS[reason] ?? reason : "closed";
  return `not closed: ${reason ?? "refused"}`;
}
