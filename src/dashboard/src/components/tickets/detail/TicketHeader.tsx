"use client";

import { CancelRunButton } from "@/components/jobs/CancelRunButton";
import { useLiveAttempt } from "@/lib/tickets/TicketPageStore";
import { EventType } from "@/types/hub-events";
import type { TicketDetail } from "@/types/tickets";
import { ticketDot } from "../ticketDot";
import { pullRequestFate, ticketLabel } from "../ticketFormat";
import { statusWords } from "./flowWords";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59q: the mock's .t-head — key and title, then a subline of what is recorded: the
// tracker link, the tracker status as the newest attempt read it, the live (or newest) attempt, how the
// pull requests ended and the branch, each only when present. The dot is the list row's dot. Cancel ends the live attempt; its cancelling state comes from a
// RunCancelRequested in that attempt's stream.

const PLATFORM_WORDS: Record<string, string> = {
  github: "GitHub",
  gitlab: "GitLab",
  jira: "Jira",
  azuredevops: "Azure DevOps",
};

export function TicketHeader({ detail }: { detail: TicketDetail }) {
  const { summary, attempts, branch } = detail;
  const live = useLiveAttempt();
  const address = useTicketTabGo();
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const cancelling = live.events.some((e) => e.type === EventType.RunCancelRequested);
  const parts: React.ReactNode[] = [];
  if (summary.webUrl) {
    const platform = summary.platform ? PLATFORM_WORDS[summary.platform.toLowerCase()] ?? summary.platform : "tracker";
    parts.push(<a key="tracker" href={summary.webUrl} target="_blank" rel="noreferrer">{platform} ↗</a>);
  }
  if (summary.trackerState && newest) {
    parts.push(<span key="state">tracker status <b>{summary.trackerState}</b> at attempt {newest.number}</span>);
  }
  if (live.attempt) parts.push(<span key="live">attempt {live.attempt.number} {statusWords(live.attempt.status)}</span>);
  else if (newest) parts.push(<span key="newest">attempt {newest.number} {statusWords(newest.status)}</span>);
  const fate = pullRequestFate(summary.pullRequests ?? []);
  if (fate) parts.push(<span key="prs" data-testid="ticket-pr-fate">{fate}</span>);
  if (branch) parts.push(<span key="branch">branch <span className="mono">{branch}</span></span>);

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
      </div>
      <div className="t-act">
        <button type="button" className="btn primary" data-testid="ticket-continue"
          onClick={() => address.go("conversation", { [CAUSE_PARAM]: null })}>Continue in conversation</button>
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

