"use client";

import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "@/components/jobs/runStatus";
import { PrButton } from "@/components/jobs/PrButton";
import { ResultTab } from "@/components/jobs/ResultTab";
import { extractUrls, stripUrls } from "@/lib/prUrls";
import { cn } from "@/lib/utils";

// p0343c: the Outcome beat's stage — the run-viewer.html outcome card bound to
// real terminal data: the summary hero, the PR link (only when a PR exists),
// the cost / wall-clock / LLM-calls .kv strip, and the cached result.md below.
// A run that has not finished shows the mock's honest "lands here" hint.

const CHECK = (
  <svg viewBox="0 0 16 16" width="22" height="22" fill="none" aria-hidden="true">
    <path
      d="M3.5 8.5l3 3 6-7"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    />
  </svg>
);

const CROSS = (
  <svg viewBox="0 0 16 16" width="22" height="22" fill="none" aria-hidden="true">
    <path d="M4 4l8 8M12 4l-8 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
  </svg>
);

function wallClock(startedAt: string, finishedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = finishedAt ? new Date(finishedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.round((end - start) / 1000));
  const minutes = Math.floor(seconds / 60);
  return `${minutes}m ${(seconds % 60).toString().padStart(2, "0")}s`;
}

export function OutcomePanel({ runId, snapshot }: { runId: string; snapshot: RunSnapshot | null }) {
  const status = snapshot ? toNodeStatus(snapshot.status) : "wait";
  const terminal = status === "ok" || status === "fail" || status === "cancel";

  if (!snapshot || !terminal) {
    return (
      <section className="card" data-testid="outcome-panel">
        <div className="card-h">
          <h3>Outcome</h3>
          <span className="badge neu">not yet</span>
        </div>
        <div className="card-b">
          <p className="hint">
            The PR, the changed-file summary and the run report land here when the run finishes.
          </p>
        </div>
      </section>
    );
  }

  const ok = status === "ok";
  // p0350: show EVERY opened PR, not just the first. Prefer the per-repo list;
  // fall back to the single prUrl for older/live snapshots that predate it.
  const basePrs =
    snapshot.pullRequests && snapshot.pullRequests.length > 0
      ? snapshot.pullRequests
      : snapshot.prUrl
      ? [{ repo: "", url: snapshot.prUrl, status: "opened", isDraft: !ok }]
      : [];
  // p0372: a PR URL embedded in the outcome message (typical for a FAILED run
  // whose draft PR survives) becomes a button too — one button per PR, and the
  // raw URL leaves the prose so the reference never renders as dead text.
  const summaryUrls = extractUrls(snapshot.summary).filter(
    (url) => !basePrs.some((pr) => pr.url === url),
  );
  const prs = [
    ...basePrs,
    ...summaryUrls.map((url) => ({ repo: "", url, status: "opened", isDraft: !ok })),
  ];
  const summaryText = snapshot.summary ? stripUrls(snapshot.summary) : null;
  const multiRepo = prs.length > 1;
  return (
    <section
      className="card"
      data-testid="outcome-panel"
      style={status === "fail" ? { borderColor: "color-mix(in srgb, var(--bad) 30%, var(--line))" } : undefined}
    >
      <div className="card-h">
        <h3>Outcome</h3>
        <span className={cn("badge", ok ? "ok" : status === "fail" ? "bad" : "neu")} data-testid="outcome-badge">
          {ok ? "✓ done" : status === "fail" ? "✗ failed" : "cancelled"}
        </span>
      </div>
      <div className="card-b">
        <div className="outcome-hero">
          <div
            className="big-ic"
            style={ok ? undefined : { background: "var(--bad-wash)", color: "var(--bad)" }}
          >
            {ok ? CHECK : CROSS}
          </div>
          <div>
            {summaryText && (
              <div style={{ fontSize: "16px", fontWeight: 640 }} data-testid="outcome-summary">
                {summaryText}
              </div>
            )}
            {prs.length > 0 && (
              <div
                className="pr-links"
                data-testid="outcome-pr-links"
                style={{ marginTop: 10, display: "flex", flexWrap: "wrap", gap: 8 }}
              >
                {/* p0372: the ONE PrButton — same component as the list rows;
                    draft PRs stay clickable, failed runs included. */}
                {prs.map((pr) => (
                  <PrButton
                    key={pr.repo + pr.url}
                    url={pr.url}
                    repo={multiRepo && pr.repo ? pr.repo : undefined}
                    isDraft={pr.isDraft}
                    testId="outcome-pr-link"
                  />
                ))}
              </div>
            )}
          </div>
        </div>
        <div className="kv" data-testid="outcome-kv">
          <div>
            <div className="k">Total cost</div>
            <div className="v num">${snapshot.costUsd.toFixed(2)}</div>
          </div>
          <div>
            <div className="k">Wall clock</div>
            <div className="v num">{wallClock(snapshot.startedAt, snapshot.finishedAt)}</div>
          </div>
          <div>
            <div className="k">LLM calls</div>
            <div className="v num">{snapshot.llmCalls}</div>
          </div>
        </div>
        <div style={{ marginTop: 16 }}>
          <ResultTab runId={runId} prUrl={snapshot.prUrl} />
        </div>
      </div>
    </section>
  );
}
