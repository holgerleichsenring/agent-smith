"use client";

import { useEffect, useRef, useState } from "react";
import { useT } from "@/hooks/useT";
import { markLandedByHand } from "@/lib/ticketGraphApi";

// 2026-10-14-dbcen: ticket-graph.html's done-by-hand dialog — a note is required, and every answer that is not a
// landing is the server's own sentence shown as is (a landing that already stands, a ticket the tracker does not
// have, an unreachable tracker). A landing closes the dialog and hands the page its re-read.

interface Props {
  project: string;
  ticketId: string;
  /** The ticket's name as a person reads it — "#58", "PROJ-41". */
  label: string;
  onClose: () => void;
  onLanded: () => void;
}

type State = { kind: "editing" } | { kind: "sending" } | { kind: "said"; text: string };

export function DoneByHandDialog({ project, ticketId, label, onClose, onLanded }: Props) {
  const t = useT();
  const [note, setNote] = useState("");
  const [state, setState] = useState<State>({ kind: "editing" });
  const field = useRef<HTMLTextAreaElement>(null);
  useEffect(() => field.current?.focus(), []);

  const send = async () => {
    setState({ kind: "sending" });
    try {
      const answer = await markLandedByHand(project, ticketId, note.trim());
      if (answer.kind === "landed") {
        onLanded();
        onClose();
      } else setState({ kind: "said", text: answer.text });
    } catch (thrown) {
      setState({ kind: "said", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
  };

  return (
    <div className="dbh-back" onClick={(e) => e.target === e.currentTarget && onClose()} data-testid="done-by-hand-backdrop">
      <div className="dbh" role="dialog" aria-modal="true" aria-labelledby={`dbh-${ticketId}`} data-testid="done-by-hand-dialog"
        onKeyDown={(e) => e.key === "Escape" && onClose()}>
        <h3 id={`dbh-${ticketId}`}>{t("tickets.landing.title", { label })}</h3>
        <div className="dbh-sub">{t("tickets.landing.sub", { label })}</div>
        <textarea ref={field} value={note} onChange={(e) => setNote(e.target.value)} aria-label={t("tickets.landing.note")}
          placeholder={t("tickets.landing.placeholder")} data-testid="done-by-hand-note" />
        {state.kind === "said" && <div className="dbh-said" data-testid="done-by-hand-said">{state.text}</div>}
        <div className="dbh-acts">
          <button type="button" className="btn" onClick={onClose}>{t("common.cancel")}</button>
          <button type="button" className="btn primary" onClick={() => void send()} data-testid="done-by-hand-send"
            disabled={note.trim() === "" || state.kind === "sending"}>
            {t("tickets.landing.send")}
          </button>
        </div>
      </div>
    </div>
  );
}
