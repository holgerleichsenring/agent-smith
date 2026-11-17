"use client";

import type { ReactNode } from "react";
import type { TicketDetail } from "@/types/tickets";
import { ticketDot } from "../ticketDot";
import { closedWords, filedWords } from "../TicketActivity";
import { isNeverRun, platformWords, pullRequestFate, ticketLabel } from "../ticketFormat";
import { TicketMoreMenu } from "./TicketMoreMenu";

// 2026-10-13-9a59q: the mock's .t-head — key and title, then one line of what is recorded.
// 2026-11-16-6877a: that line reads project, tracker, the tracker status as the newest attempt read it, the branch
// and how the pull requests ended, each only when present; every other act sits behind "···" (TicketMoreMenu).
// 2026-11-17-8d1ca: the header of a ticket with no run only; a ticket with a run has its section bar.

export function TicketHeader({ detail }: { detail: TicketDetail }) {
  const { summary, attempts, branch } = detail;
  const neverRun = attempts.length === 0 && isNeverRun(summary);
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
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
      <TicketMoreMenu summary={summary} />
    </div>
  );
}

function capitalized(words: string): string {
  return words.charAt(0).toUpperCase() + words.slice(1);
}
