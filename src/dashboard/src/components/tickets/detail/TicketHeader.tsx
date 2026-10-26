"use client";

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

// 2026-10-13-9a59q: the mock's .t-head — key and title, then a subline of what is recorded: the
// tracker link, the tracker status as the newest attempt read it, the live (or newest) attempt, how the
// pull requests ended and the branch, each only when present. The dot is the list row's dot. Cancel ends the live attempt; its cancelling state comes from a
// RunCancelRequested in that attempt's stream. 2026-10-14-dbcen: a ticket with no run says when it was filed and
// offers no conversation tab to continue in; every ticket not yet landed can be marked done by hand.
// 2026-10-15-114fd: Close ticket, or Reopen on a ticket closed here; the closed line follows the BUCKET, so a ticket
// that came back draws none though its mark is still stored.
// 2026-10-26-bdd0f: Derive specs on every ticket not closed — the specs derived and reviewed without an attempt.
// 2026-10-16-86e6c: the attempt's state, a run-bearing ticket's closed line and the primary act moved into the state
// block under the header; a never-run closed ticket draws no block and keeps its closed line here.

export function TicketHeader({ detail }: { detail: TicketDetail }) {
  const { summary, attempts, branch } = detail;
  const live = useLiveAttempt();
  const requires = useTicketRequires();
  const neverRun = attempts.length === 0 && isNeverRun(summary);
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const cancelling = live.events.some((e) => e.type === EventType.RunCancelRequested);
  const parts: React.ReactNode[] = [];
  if (summary.webUrl) {
    const platform = summary.platform ? platformWords(summary.platform) : "tracker";
    parts.push(<a key="tracker" href={summary.webUrl} target="_blank" rel="noreferrer">{platform} ↗</a>);
  }
  if (summary.trackerState && newest) {
    parts.push(<span key="state">tracker status <b>{summary.trackerState}</b> at attempt {newest.number}</span>);
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
      <div className="t-act">
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
      </div>
    </div>
  );
}

function capitalized(words: string): string {
  return words.charAt(0).toUpperCase() + words.slice(1);
}
