"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { fetchPullRequests } from "@/lib/pullRequestsApi";
import type { PullRequest } from "@/types/hub-events";
import { cn } from "@/lib/utils";

// p0347: agent-smith's OUTPUT gets its own surface. The Pull Requests page lists
// every PR the agent opened — per repo, so multi-repo runs keep all their PRs —
// each linking OUT to the provider and BACK to its run/ticket. Opened PRs are
// the headline (metric strip + .rrow rows with a green status pill + external
// link); no_changes/failed attempts are an honest MUTED section below so an
// operator sees where a run produced NO pr and why. Pixel identity: the
// runs-list.html mock's .mock-shell/.mock-runs vocabulary — .health strip,
// .section-head rules, .rows of .rrow rows, .pill status chips. No new dialect.

function relativeAgo(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "";
  const seconds = Math.max(0, Math.round((Date.now() - then) / 1000));
  if (seconds < 45) return "just now";
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.round(hours / 24);
  return `${days}d ago`;
}

function isToday(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  );
}

function withinDays(iso: string, days: number): boolean {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return false;
  return Date.now() - then <= days * 24 * 60 * 60 * 1000;
}

export default function PullRequestsPage() {
  const [prs, setPrs] = useState<PullRequest[] | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    fetchPullRequests(controller.signal)
      .then((rows) => setPrs(rows))
      .catch((e) => {
        if (!controller.signal.aborted) setError(true);
        void e;
      });
    return () => controller.abort();
  }, []);

  const opened = useMemo(() => (prs ?? []).filter((p) => p.status === "opened"), [prs]);
  const attempts = useMemo(
    () => (prs ?? []).filter((p) => p.status !== "opened"),
    [prs],
  );

  const openToday = opened.filter((p) => isToday(p.openedAt)).length;
  const openWeek = opened.filter((p) => withinDays(p.openedAt, 7)).length;

  return (
    <div className="mock-shell mock-runs" data-testid="pull-requests-page">
      <main className="main">
        <div className="m-head">
          <div>
            <h1>Pull requests</h1>
            <div className="msub">
              Every PR agent-smith opened — per repo, linked out and back to its run.
            </div>
          </div>
        </div>

        {/* metric strip — the honest recorded-at-open counts */}
        <div
          className="health"
          data-testid="pr-metric-strip"
          style={{ gridTemplateColumns: "repeat(3, 1fr)" }}
        >
          <Metric label="Open today" value={openToday} testId="pr-metric-today" />
          <Metric label="Open this week" value={openWeek} testId="pr-metric-week" />
          <Metric label="Total open" value={opened.length} testId="pr-metric-total" />
        </div>

        {prs === null ? (
          error ? (
            <div className="rows" data-testid="pr-error">
              <div className="rrow" style={{ cursor: "default", justifyContent: "center", display: "flex" }}>
                Couldn’t load pull requests. Retry from the rail once the API is reachable.
              </div>
            </div>
          ) : (
            <div className="rows" data-testid="pr-skeleton">
              {[0, 1, 2].map((i) => (
                <div key={i} className="rrow h-14 animate-pulse" style={{ cursor: "default" }} />
              ))}
            </div>
          )
        ) : (
          <>
            <OpenedSection opened={opened} />
            <AttemptsSection attempts={attempts} />
            {opened.length === 0 && attempts.length === 0 && (
              <div className="rows" data-testid="pr-empty">
                <div
                  className="rrow"
                  style={{ cursor: "default", justifyContent: "center", display: "flex" }}
                >
                  No pull requests yet. When a run commits changes, its PR shows up here.
                </div>
              </div>
            )}
          </>
        )}
      </main>
    </div>
  );
}

function Metric({ label, value, testId }: { label: string; value: number; testId: string }) {
  return (
    <div className="metric" data-testid={testId}>
      <span className="k">{label}</span>
      <span className="v num">{value}</span>
    </div>
  );
}

// The HEADLINE: opened PRs as .rrow rows, each linking OUT and BACK.
function OpenedSection({ opened }: { opened: PullRequest[] }) {
  if (opened.length === 0) return null;
  return (
    <section data-testid="pr-opened-section" className="scroll-mt-6">
      <div className="section-head">
        <h2>Opened</h2>
        <span className="cnt" data-testid="pr-opened-count">
          {opened.length}
        </span>
        <span className="sh-sub">what the agent actually shipped — click ↗ to review</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="rows">
        {opened.map((pr) => (
          <OpenedRow key={`${pr.runId}:${pr.repo}`} pr={pr} />
        ))}
      </div>
    </section>
  );
}

function OpenedRow({ pr }: { pr: PullRequest }) {
  const tick = pr.ticketId ? `#${pr.ticketId}` : `#${pr.runId.slice(0, 8)}`;
  const title = pr.ticketTitle ?? pr.pipeline;
  return (
    <div
      className="rrow st-ok"
      data-testid={`pr-row-${pr.runId}-${pr.repo}`}
      style={{
        cursor: "default",
        gridTemplateColumns: "14px minmax(0, 1fr) auto auto auto auto",
      }}
    >
      <span className="sd" />

      <div className="rmain">
        <div className="rt">
          <span className="tick">{pr.repo}</span>
          <span className="ttl">{title}</span>
        </div>
        <div className="act">
          <b>{tick}</b> · {pr.pipeline}
        </div>
      </div>

      <span className="pill ok" data-testid={`pr-row-${pr.runId}-${pr.repo}-status`}>
        opened
      </span>

      <span className="prog hidesm">{relativeAgo(pr.openedAt)}</span>

      {pr.url ? (
        <a
          className="pill ok"
          href={pr.url}
          target="_blank"
          rel="noreferrer"
          data-testid={`pr-row-${pr.runId}-${pr.repo}-link`}
          style={{ textTransform: "none", cursor: "pointer" }}
        >
          Pull request ↗
        </a>
      ) : (
        <span className="prog hidesm" />
      )}

      <Link
        className="prog"
        href={`/jobs/${encodeURIComponent(pr.runId)}`}
        data-testid={`pr-row-${pr.runId}-${pr.repo}-run`}
        style={{ cursor: "pointer" }}
      >
        run ›
      </Link>
    </div>
  );
}

// The honest MUTED section: no_changes / failed attempts — where a run produced
// NO pr, and why. Each row links back to its run.
function AttemptsSection({ attempts }: { attempts: PullRequest[] }) {
  if (attempts.length === 0) return null;
  return (
    <section data-testid="pr-attempts-section" className="scroll-mt-6" style={{ opacity: 0.85 }}>
      <div className="section-head">
        <h2>No PR</h2>
        <span className="cnt" data-testid="pr-attempts-count">
          {attempts.length}
        </span>
        <span className="sh-sub">runs that produced no pull request — and why</span>
      </div>
      <div style={{ height: 14 }} />
      <div className="rows">
        {attempts.map((pr) => (
          <AttemptRow key={`${pr.runId}:${pr.repo}`} pr={pr} />
        ))}
      </div>
    </section>
  );
}

function AttemptRow({ pr }: { pr: PullRequest }) {
  const tick = pr.ticketId ? `#${pr.ticketId}` : `#${pr.runId.slice(0, 8)}`;
  const failed = pr.status === "failed";
  const reason =
    pr.reason ?? (pr.status === "no_changes" ? "No changes to commit" : "PR could not be opened");
  return (
    <Link
      className={cn("rrow", failed ? "st-bad" : "st-q")}
      href={`/jobs/${encodeURIComponent(pr.runId)}`}
      data-testid={`pr-attempt-${pr.runId}-${pr.repo}`}
      style={{ gridTemplateColumns: "14px minmax(0, 1fr) auto 14px" }}
    >
      <span className="sd" />
      <div className="rmain">
        <div className="rt">
          <span className="tick">{pr.repo}</span>
          <span className="ttl">{pr.ticketTitle ?? pr.pipeline}</span>
        </div>
        <div className="act">
          <b>{tick}</b> · {reason}
        </div>
      </div>
      <span
        className={cn("pill", failed ? "bad" : "q")}
        data-testid={`pr-attempt-${pr.runId}-${pr.repo}-status`}
      >
        {failed ? "failed" : "no changes"}
      </span>
      <span className="chev">›</span>
    </Link>
  );
}
