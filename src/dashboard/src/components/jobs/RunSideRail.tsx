"use client";

import { useState } from "react";
import type { RunPullRequest, RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "./runStatus";
import { cn } from "@/lib/utils";

// p0343b/p0343c (pixel identity): the run-detail side rail — the run-viewer
// mock's .sidebox verbatim: the vertical .health metric stack (State /
// Progress / Compute / Cost / Elapsed), the expandable .pods detail (only when
// the run carries a p0336 footprint), then the two .trace-btn entry points:
// "Dialogue" ONLY when a real pending question exists, and "Full pipeline"
// opening the trace drawer. Every block renders REAL snapshot data and is
// honestly absent otherwise.

const STATE_LABEL: Record<string, string> = {
  waiting_for_input: "Needs you",
  running: "Running",
  queued: "Queued",
  success: "Done",
  failed: "Failed",
  error: "Failed",
  cancelled: "Cancelled",
};

function money(usd: number): string {
  return `$${usd.toFixed(2)}`;
}

// p0347: the PR block panel — the health-strip look (panel bg, hairline, radius)
// expressed with the same tokens, no new stylesheet dialect.
const PR_BOX: React.CSSProperties = {
  display: "flex",
  flexDirection: "column",
  gap: 8,
  background: "var(--panel)",
  border: "1px solid var(--line)",
  borderRadius: "var(--r)",
  boxShadow: "var(--shadow)",
  padding: "12px 13px",
};

const PR_BOX_HEAD: React.CSSProperties = {
  fontSize: "10px",
  letterSpacing: "0.12em",
  textTransform: "uppercase",
  color: "var(--ink-3)",
  fontWeight: 600,
};

function duration(startedAt: string, finishedAt: string | null): string {
  const start = new Date(startedAt).getTime();
  const end = finishedAt ? new Date(finishedAt).getTime() : Date.now();
  const seconds = Math.max(0, Math.round((end - start) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  return `${minutes}m ${(seconds % 60).toString().padStart(2, "0")}s`;
}

// p0347: the OPENED PRs to surface in the rail. Prefer the per-repo projection
// (multi-repo runs keep every PR); an old run that predates it contributes its
// single prUrl as one opened row so history isn't blank. Only entries with a
// real url are returned — an "opened" status with no url is not linkable.
function openedPullRequests(snapshot: RunSnapshot): RunPullRequest[] {
  const projected = (snapshot.pullRequests ?? []).filter(
    (pr) => pr.status === "opened" && !!pr.url,
  );
  if (projected.length > 0) return projected;
  if (snapshot.prUrl) {
    return [
      {
        repo: snapshot.repos[0] ?? "repository",
        status: "opened",
        url: snapshot.prUrl,
        reason: null,
        openedAt: snapshot.finishedAt ?? snapshot.startedAt,
      },
    ];
  }
  return [];
}

export function RunSideRail({
  snapshot,
  hasDialogue,
  onOpenDialogue,
  onOpenTrace,
  traceSteps,
}: {
  snapshot: RunSnapshot;
  /** True only when a REAL pending question exists on the run. */
  hasDialogue: boolean;
  onOpenDialogue: () => void;
  onOpenTrace: () => void;
  /** Step count for the "Full pipeline · N steps" label (0 = unknown). */
  traceSteps: number;
}) {
  const status = toNodeStatus(snapshot.status);
  const footprint = snapshot.footprint ?? null;
  const [podsOpen, setPodsOpen] = useState(false);

  const stateLabel = STATE_LABEL[snapshot.status.toLowerCase()] ?? snapshot.status.replaceAll("_", " ");

  // p0347: the run's deliverable, surfaced PROMINENTLY — every OPENED PR (a
  // multi-repo run keeps them all), each linking straight out to the provider,
  // high in the rail so the operator reaches it without opening a beat. Old runs
  // that predate the per-repo projection fall back to their single prUrl.
  const openedPrs = openedPullRequests(snapshot);
  const isFinished = status === "ok" || status === "fail" || status === "cancel";

  return (
    <aside className="sidebox" data-testid="run-side-rail">
      <div className="health">
        <div className="metric">
          <span className="k">State</span>
          <span
            className="v"
            data-testid="side-rail-state"
            style={{
              color:
                status === "ok"
                  ? "var(--ok)"
                  : status === "fail"
                  ? "var(--bad)"
                  : status === "run" || status === "queued" || status === "input"
                  ? "var(--run)"
                  : undefined,
              fontSize: "14.5px",
            }}
          >
            {stateLabel}
          </span>
        </div>

        {snapshot.totalSteps > 0 && (
          <div className="metric">
            <span className="k">Progress</span>
            <span className="v" data-testid="side-rail-progress">
              {snapshot.stepIndex}
              <small>of {snapshot.totalSteps} steps</small>
            </span>
          </div>
        )}

        {/* Honest omission: no footprint on the row → no COMPUTE block. */}
        {footprint && (
          <button
            type="button"
            className="metric compute"
            title="Show compute detail"
            data-testid="side-rail-compute"
            onClick={() => setPodsOpen((v) => !v)}
          >
            <span className="k">Compute</span>
            <span className="v">
              <span className="cdot" />
              <span data-testid="side-rail-compute-v">
                {footprint.pods.length} {footprint.pods.length === 1 ? "pod" : "pods"} ·{" "}
                {footprint.totalMemLimit}
              </span>
            </span>
          </button>
        )}

        <div className="metric">
          <span className="k">Cost</span>
          <span className="v num" data-testid="side-rail-cost">
            {money(snapshot.costUsd)}
          </span>
        </div>

        <div className="metric">
          <span className="k">Elapsed</span>
          <span className="v num" data-testid="side-rail-elapsed">
            {duration(snapshot.startedAt, snapshot.finishedAt)}
            <small>· {snapshot.llmCalls} LLM</small>
          </span>
        </div>
      </div>

      {/* p0347: PR block — high in the rail, just under the metric strip. Present
          only when the run really opened a PR; a finished run that opened none
          shows an honest muted line instead of an empty promise. */}
      {openedPrs.length > 0 ? (
        <div style={PR_BOX} data-testid="side-rail-prs">
          <div style={PR_BOX_HEAD}>
            {openedPrs.length === 1 ? "Pull request" : `Pull requests · ${openedPrs.length}`}
          </div>
          {openedPrs.map((pr) => (
            <a
              key={pr.repo}
              className="trace-btn"
              href={pr.url!}
              target="_blank"
              rel="noreferrer"
              data-testid={`side-rail-pr-${pr.repo}`}
              style={{ color: "var(--accent)", justifyContent: "flex-start" }}
            >
              <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
                <path d="M6 3h7v7M13 3L4 12" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
              </svg>
              <span
                className="mono"
                style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
              >
                {pr.repo}
              </span>
              <span style={{ marginLeft: "auto" }}>↗</span>
            </a>
          ))}
        </div>
      ) : (
        isFinished && (
          <div style={PR_BOX} data-testid="side-rail-prs-none">
            <div style={PR_BOX_HEAD}>Pull requests</div>
            <div style={{ fontSize: "12.5px", color: "var(--ink-3)" }}>
              No PR opened — see the Outcome beat for why.
            </div>
          </div>
        )
      )}

      {footprint && (
        <div className={cn("pods", !podsOpen && "closed")} data-testid="side-rail-pods">
          <div className="ph">
            {footprint.reserved
              ? "Reserved at admission · held for the whole run"
              : footprint.reason}
          </div>
          {footprint.pods.map((pod) => (
            <div className="pod" key={pod.repo}>
              <div>
                <span className="p-name">{pod.repo}</span>
                <br />
                <span className="p-img">{pod.image}</span>
              </div>
              <div className="p-res">
                {pod.memLimit} · {pod.cpuLimit}
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Dialogue exists ONLY when the run really waits on a question. */}
      {hasDialogue && (
        <button
          type="button"
          className="trace-btn"
          data-testid="side-rail-dialogue"
          onClick={onOpenDialogue}
        >
          <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
            <path d="M3 3h10v7H8l-3 3v-3H3z" stroke="currentColor" strokeWidth="1.5" strokeLinejoin="round" />
          </svg>
          Dialogue <span className="dlg-cnt">1 open</span>
        </button>
      )}
      <button
        type="button"
        className="trace-btn"
        data-testid="side-rail-pipeline-jump"
        onClick={onOpenTrace}
      >
        <svg width="14" height="14" viewBox="0 0 16 16" fill="none" aria-hidden="true">
          <path d="M2 4h12M2 8h12M2 12h8" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
        </svg>
        Full pipeline{traceSteps > 0 ? ` · ${traceSteps} steps` : ""}
      </button>
    </aside>
  );
}
