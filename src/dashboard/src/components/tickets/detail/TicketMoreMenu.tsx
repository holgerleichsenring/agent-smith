"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { useLiveAttempt, useTicketEdited, useTicketRequires } from "@/lib/tickets/TicketPageStore";
import { EventType } from "@/types/hub-events";
import type { TicketSummary } from "@/types/tickets";
import { platformWords, ticketLabel } from "../ticketFormat";
import { DoneByHandAct } from "../landing/DoneByHandAct";
import { TicketCloseAct } from "../closing/TicketCloseAct";
import { TicketReopenAct } from "../closing/TicketReopenAct";
import { DeriveSpecsAct } from "./DeriveSpecsAct";
import { DeployAct } from "../deploying/DeployAct";

// 2026-11-16-6877a: every act that is not the next step sits behind "···": open in the tracker, Done by hand, Close
// or Reopen, Derive specs with where the specs go. The menu stays open until "···" or Escape closes it, so a dialog
// an act opens is not torn down. 2026-11-17-8d1ca: out of the header, so the ticket's heading and the never-run
// header share it; Cancel for a live attempt left the menu and stands beside it (LiveAttemptCancel).

export function TicketMoreMenu({ summary }: { summary: TicketSummary }) {
  const requires = useTicketRequires();
  const edited = useTicketEdited();
  return (
    <MoreMenu>
      {summary.webUrl && (
        <a className="tk-menu-item" href={summary.webUrl} target="_blank" rel="noreferrer" data-testid="ticket-open-tracker">
          Open in {platformWords(summary.platform)} ↗
        </a>
      )}
      <DoneByHandAct project={summary.project} ticketId={summary.ticketId} label={ticketLabel(summary)}
        landed={summary.bucket === "done"} onLanded={requires.landed} />
      <TicketCloseAct ticket={summary} onClosed={requires.landed} />
      <TicketReopenAct ticket={summary} onReopened={requires.landed} />
      {summary.bucket !== "closed" && <DeriveSpecsAct project={summary.project} ticketId={summary.ticketId} />}
      {/* 2026-11-18-3863d: the started deploy run is listed among the ticket's runs once the page re-reads. */}
      <DeployAct project={summary.project} ticketId={summary.ticketId} onStarted={edited} />
    </MoreMenu>
  );
}

/** The live attempt's Cancel, in plain sight; nothing while no attempt is live. */
export function LiveAttemptCancel() {
  const live = useLiveAttempt();
  if (!live.attempt) return null;
  const cancelling = live.events.some((e) => e.type === EventType.RunCancelRequested);
  return (
    <span className="tk-cancel">
      <CancelRunButton runId={live.attempt.runId} cancelRequested={cancelling} label={`Cancel run ${live.attempt.number}`} />
    </span>
  );
}

function MoreMenu({ children }: { children: ReactNode }) {
  const [open, setOpen] = useState(false);
  const button = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      setOpen(false);
      button.current?.focus();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);
  return (
    <div className="t-act tk-more">
      <button ref={button} type="button" className="tk-more-btn" aria-label="More actions" aria-expanded={open}
        aria-controls="ticket-more" onClick={() => setOpen(!open)} data-testid="ticket-more">···</button>
      <div className="tk-menu" id="ticket-more" role="group" aria-label="More actions" hidden={!open} data-testid="ticket-more-menu">
        {children}
      </div>
    </div>
  );
}
