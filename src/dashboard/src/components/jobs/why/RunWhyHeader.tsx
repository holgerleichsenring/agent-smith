"use client";

import { shortRunId } from "@/lib/runId";
import Link from "next/link";
import type { RunSnapshot } from "@/types/hub-events";

// p0341h: the same header the run itself wears — a back link, the ticket as the heading,
// and the joined identity strip. It used to be a bare h1 with a button beside it, which
// read as a different application one click away from the run. The page is a second view
// OF a run, so it carries the run's identity in the run's own idiom; only the back link
// and the phrase differ, because that is all that actually differs.

export function RunWhyHeader({ runId, snapshot }: { runId: string; snapshot: RunSnapshot | null }) {
  const repoNames = snapshot?.repos ?? [];
  return (
    <header>
      <Link className="back" href={`/jobs/${encodeURIComponent(runId)}`} data-testid="run-why-back">
        <svg width="13" height="13" viewBox="0 0 16 16" fill="none" aria-hidden="true">
          <path
            d="M9.5 3.5L5 8l4.5 4.5"
            stroke="currentColor"
            strokeWidth="1.6"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        Back to the run
      </Link>
      <div className="head-row">
        <div className="head-main">
          <h1 data-testid="run-why-heading" title={snapshot?.ticketTitle ?? "Why this run did that"}>
            {snapshot?.ticketTitle ?? "Why this run did that"}
          </h1>
          <div className="statusline">
            <span className="phrase">Why this run did that</span>
          </div>
        </div>
        <div className="ident" data-testid="run-why-ident">
          <div className="f">
            <span className="fl">Run</span>
            <span className="fv" title={runId}>
              #{shortRunId(runId)}
            </span>
          </div>
          {snapshot?.ticketId && (
            <div className="f">
              <span className="fl">Ticket</span>
              <span className="fv">{snapshot.ticketId}</span>
            </div>
          )}
          {snapshot?.pipeline && (
            <div className="f">
              <span className="fl">Pipeline</span>
              <span className="fv">{snapshot.pipeline}</span>
            </div>
          )}
          {snapshot?.agentName && (
            <div className="f">
              <span className="fl">Agent</span>
              <span className="fv">{snapshot.agentName}</span>
            </div>
          )}
          {repoNames.length > 0 && (
            <div className="f">
              <span className="fl">Repositories</span>
              <span className="fv" title={repoNames.join(", ")}>
                {repoNames.length === 1
                  ? repoNames[0]
                  : `${repoNames.length} · ${repoNames.join(", ")}`}
              </span>
            </div>
          )}
        </div>
      </div>
    </header>
  );
}
