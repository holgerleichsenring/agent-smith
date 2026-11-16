"use client";

import { useEffect, useRef, useState, type ReactNode } from "react";
import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { useLiveAttempt, useTicketRequires } from "@/lib/tickets/TicketPageStore";
import { EventType } from "@/types/hub-events";
import type { TicketDetail } from "@/types/tickets";
import { ticketDot } from "../ticketDot";
import { closedWords, filedWords } from "../TicketActivity";
import { isNeverRun, platformWords, pullRequestFate, ticketLabel } from "../ticketFormat";
import { DoneByHandAct } from "../landing/DoneByHandAct";
import { TicketCloseAct } from "../closing/TicketCloseAct";
import { TicketReopenAct } from "../closing/TicketReopenAct";
import { DeriveSpecsAct } from "./DeriveSpecsAct";

// 2026-10-13-9a59q: the mock's .t-head — key and title, then one line of what is recorded.
// 2026-11-16-6877a: that line reads project, tracker, the tracker status as the newest attempt read it, the branch
// and how the pull requests ended, each only when present. Every act that is not the next step sits behind "···":
// open in the tracker, Done by hand, Close or Reopen, Derive specs with where the specs go, and Cancel for a live
// attempt. The menu stays open until "···" or Escape closes it, so a dialog an act opens is not torn down.

export function TicketHeader({ detail }: { detail: TicketDetail }) {
  const { summary, attempts, branch } = detail;
  const live = useLiveAttempt();
  const requires = useTicketRequires();
  const neverRun = attempts.length === 0 && isNeverRun(summary);
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const cancelling = live.events.some((e) => e.type === EventType.RunCancelRequested);
  const tracker = platformWords(summary.platform);
  const parts: ReactNode[] = [<span key="project">{summary.project}</span>];
  if (summary.platform || summary.ticketKey) parts.push(<span key="tracker">{summary.platform ? `${tracker} ` : ""}{ticketLabel(summary)}</span>);
  if (summary.trackerState && newest) {
    parts.push(<span key="state">{summary.platform ? tracker : "tracker"} status <b>{summary.trackerState}</b> at attempt {newest.number}</span>);
  }
  if (neverRun) parts.push(<span key="filed">{filedWords(summary.since)}</span>);
  const fate = pullRequestFate(summary.pullRequests ?? []);
  if (fate) parts.push(<span key="prs" data-testid="ticket-pr-fate">{fate}</span>);
  if (branch && !neverRun) parts.push(<span key="branch">branch <span className="mono">{branch}</span></span>);

  return (
    <div className="t-head" data-testid="ticket-head">
      <span className={`ticon ${ticketDot(summary)}`} aria-hidden="true" />
      <div className="tt">
        <h1>
          <span className="key">{ticketLabel(summary)}</span>
          {summary.title ?? summary.ticketId}
        </h1>
        <div className="tsub">
          {parts.flatMap((part, i) => (i === 0 ? [part] : [<span key={`dot-${i}`}>·</span>, part]))}
        </div>
        {neverRun && summary.bucket === "closed" && <div className="closed-line" data-testid="ticket-closed-line">{capitalized(closedWords(summary))}</div>}
      </div>
      <MoreMenu>
        {summary.webUrl && (
          <a className="tk-menu-item" href={summary.webUrl} target="_blank" rel="noreferrer" data-testid="ticket-open-tracker">
            Open in {tracker} ↗
          </a>
        )}
        <DoneByHandAct project={summary.project} ticketId={summary.ticketId} label={ticketLabel(summary)}
          landed={summary.bucket === "done"} onLanded={requires.landed} />
        <TicketCloseAct ticket={summary} onClosed={requires.landed} />
        <TicketReopenAct ticket={summary} onReopened={requires.landed} />
        {summary.bucket !== "closed" && <DeriveSpecsAct project={summary.project} ticketId={summary.ticketId} />}
        {live.attempt && (
          <CancelRunButton
            runId={live.attempt.runId}
            cancelRequested={cancelling}
            label={`Cancel attempt ${live.attempt.number}`}
          />
        )}
      </MoreMenu>
    </div>
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

function capitalized(words: string): string {
  return words.charAt(0).toUpperCase() + words.slice(1);
}
