"use client";

import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { PrButton } from "@/components/jobs/PrButton";
import { isRelaunching, relaunchPlace } from "@/components/jobs/runStatus";
import { formatRunSummary } from "@/lib/formatRunSummary";
import type { RunSnapshot } from "@/types/hub-events";

// 2026-11-16-6877b: one sentence that says where the run is. A run waiting on a person shows the question card itself
// (the same card the old dialogue drawer held); a finished run says how it ended in its own words, with its pull
// requests beside the sentence.

export type RunTone = "ok" | "run" | "amber" | "bad" | "quiet";

export function runStatusWords(snapshot: RunSnapshot): { tone: RunTone; text: string } {
  const status = (snapshot.status ?? "").toLowerCase();
  const said = snapshot.summary ? formatRunSummary(snapshot.summary).trim() : "";
  if (isRelaunching(snapshot)) {
    const place = relaunchPlace(snapshot);
    return { tone: "run", text: place != null ? `Resuming, place ${place} in line. Nothing for you to do.` : "Resuming. Nothing for you to do." };
  }
  switch (status) {
    case "running":
      return { tone: "run", text: snapshot.stepName ? `Working: ${snapshot.stepName}. Nothing for you to do.` : "Working. Nothing for you to do." };
    case "queued":
      return { tone: "run", text: snapshot.queuePosition ? `Waiting for capacity, place ${snapshot.queuePosition} in line.` : "Waiting for capacity." };
    case "waiting_for_input":
      return { tone: "amber", text: "Waiting for your answer." };
    case "success":
      return { tone: "ok", text: said || "Delivered." };
    case "shortfall":
      return { tone: "amber", text: said || "Delivered with a shortfall." };
    case "cancelled":
      return { tone: "quiet", text: said ? `Cancelled. ${said}` : "Cancelled." };
    case "":
      return { tone: "quiet", text: "Reading the run…" };
    default:
      return { tone: "bad", text: said || "Stopped without a reason on record." };
  }
}

export function RunStatusBox({ snapshot }: { snapshot: RunSnapshot | null }) {
  if (!snapshot) return null;
  const { tone, text } = runStatusWords(snapshot);
  const question = snapshot.status === "waiting_for_input" && !isRelaunching(snapshot) ? snapshot.pendingQuestion ?? null : null;
  const prs = (snapshot.pullRequests ?? []).filter((p) => p.url);
  const note = statusNote(snapshot, snapshot.status === "waiting_for_input" && !isRelaunching(snapshot), prs.length > 0);
  return (
    <section className={`rl-status ${tone}`} data-testid="run-status" data-tone={tone} aria-live="polite">
      <span className="rl-dot" aria-hidden="true" />
      <div className="rl-status-say">
        <div className="rl-status-text" data-testid="run-status-text">{text}</div>
        {note && <div className="rl-status-note" data-testid="run-status-note">{note}</div>}
        {question && (
          <div className="rl-question" data-testid="run-question">
            <PendingQuestionCard runId={snapshot.runId} question={question} />
          </div>
        )}
      </div>
      {prs.length > 0 && (
        <span className="rl-prs">
          {prs.map((p) => <PrButton key={p.url} url={p.url} repo={prs.length > 1 ? p.repo : null} isDraft={p.isDraft} testId="run-status-pr" />)}
        </span>
      )}
    </section>
  );
}

const FINISHED = new Set(["success", "shortfall", "failed", "error", "cancelled"]);

/** 2026-11-17-d6fbb: what the graph view's banner and side rail added — a waiting run holds its compute and burns no
 *  tokens; a finished run that opened no pull request says so. */
export function statusNote(snapshot: RunSnapshot, asking: boolean, hasPullRequest: boolean): string | null {
  if (asking) return "Reserved compute is held; no tokens are spent while it waits.";
  const status = (snapshot.status ?? "").toLowerCase();
  if (FINISHED.has(status) && !hasPullRequest && !snapshot.prUrl) return "No pull request opened; the Outcome section says why.";
  return null;
}
