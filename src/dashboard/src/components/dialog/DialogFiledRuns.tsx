"use client";

import Link from "next/link";
import type {
  FiledWorkHandback,
  FiledWorkPhase,
  FiledWorkReview,
  FiledWorkRun,
} from "@/types/spec-dialog";

// 2026-09-17-042ej: what became of one filed ticket — the runs that took it up, newest first,
// and under each the PHASES, which is the unit an operator reasons in. A record has no run and
// renders nothing here.
//
// Three things are deliberately not softened. A phase's VERDICT is shown where the row is
// terminal, because the projection keeps the last verdict it saw — and what to DO about it is
// read off the status, never off the verdict text: a `failed` row is a red build and is fixed
// by working the code, while `handed_back` (2026-09-17-0e79c) is a false premise and is fixed
// by amending the specification in this conversation. Telling an operator whose build went red
// to approve the set again would be advice that changes nothing.
// A review that was NOT TAKEN says so with its reason instead of reading as a clean one — that
// is the whole reason the report is an object and not a list of findings, and a row that could
// not be READ says that too rather than disappearing. And a finding whose fix pass was
// REVERTED says so, because the branch still carries the code the finding is about.

/** RunPhaseProjection.StatusOf's word for a phase handed back on a false premise. */
const HANDED_BACK = "handed_back";

export function DialogFiledRuns({
  runs,
  handback,
  reference,
}: {
  runs: FiledWorkRun[];
  handback: FiledWorkHandback | null;
  reference: string;
}) {
  if (runs.length === 0 && handback === null) return null;
  return (
    <div data-testid={`dialog-filed-work-${reference}`} className="mt-1 flex flex-col gap-2">
      {handback && (
        <p data-testid={`dialog-filed-handback-${reference}`} className="dsh-label text-ink">
          handed back: <span className="font-semibold">{handback.case}</span>
          {handback.repeated > 1 && ` — ${handback.repeated} times in a row`}
          {" — the question is on the ticket."}
        </p>
      )}
      {runs.map((run) => (
        <Run key={run.runId} run={run} />
      ))}
    </div>
  );
}

function Run({ run }: { run: FiledWorkRun }) {
  return (
    <div data-testid={`dialog-filed-run-${run.runId}`} className="rounded-sm bg-canvas-soft p-2">
      <div className="flex flex-wrap items-baseline gap-x-2 dsh-label text-body">
        <Link
          href={`/jobs/${encodeURIComponent(run.runId)}`}
          className="font-mono font-semibold text-primary-deep underline hover:text-primary-pressed"
        >
          {run.runId}
        </Link>
        <span className="text-ink">{run.status}</span>
        <span>
          in {run.project} · ${run.costUsd.toFixed(2)}
        </span>
      </div>
      {run.pendingQuestion && (
        <p data-testid={`dialog-filed-waiting-${run.runId}`} className="mt-1 dsh-label text-ink">
          waiting for an answer: {run.pendingQuestion.text}
        </p>
      )}
      {run.pullRequests.length > 0 && (
        <ul className="mt-1 flex flex-wrap gap-x-3 dsh-label">
          {run.pullRequests.map((pr) => (
            <li key={pr.repo}>
              {pr.url ? (
                <a
                  href={pr.url}
                  target="_blank"
                  rel="noreferrer"
                  className="text-primary-deep underline hover:text-primary-pressed"
                >
                  {pr.repo}
                </a>
              ) : (
                <span className="text-body">
                  {pr.repo}: {pr.status}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
      {run.phases.length > 0 && (
        <ol className="mt-1 flex flex-col gap-1">
          {run.phases.map((phase) => (
            <Phase key={phase.phaseId} phase={phase} />
          ))}
        </ol>
      )}
    </div>
  );
}

function Phase({ phase }: { phase: FiledWorkPhase }) {
  return (
    <li data-testid={`dialog-filed-phase-${phase.phaseId}`} className="dsh-label">
      <span className="font-mono font-semibold text-ink">{phase.phaseId}</span>
      <span className="ml-2 text-body">{phase.title}</span>
      <span className="ml-2 text-ink">{phase.status}</span>
      {phase.verdict && (
        <div data-testid={`dialog-filed-verdict-${phase.phaseId}`} className="text-body">
          {phase.verdict}
        </div>
      )}
      {phase.status === HANDED_BACK && (
        <div data-testid={`dialog-filed-amend-${phase.phaseId}`} className="text-ink">
          the specification is what has to change — approve the set again in this conversation.
        </div>
      )}
      <Review phaseId={phase.phaseId} review={phase.review} />
    </li>
  );
}

function Review({ phaseId, review }: { phaseId: string; review: FiledWorkReview | null }) {
  // No row at all is a phase that never reached its review — the fourth state, and it says so
  // rather than staying silent, because silence is what a clean review would look like too.
  if (review === null) {
    return (
      <div data-testid={`dialog-filed-noreview-${phaseId}`} className="text-body">
        not reviewed yet
      </div>
    );
  }
  return (
    <>
      {review.unreadable && (
        <div data-testid={`dialog-filed-unreadable-${phaseId}`} className="text-ink">
          {review.why}
        </div>
      )}
      {/* A report can say "not taken" AND still carry findings, so the note and the list are
          not alternatives; only a review that was taken and kept nothing is the quiet case. */}
      {!review.reviewed && !review.unreadable && (
        <div data-testid={`dialog-filed-unreviewed-${phaseId}`} className="text-ink">
          not reviewed: {review.why ?? "no reason was recorded"}
        </div>
      )}
      {review.reviewed && review.findings.length === 0 && (
        <div data-testid={`dialog-filed-reviewed-${phaseId}`} className="text-body">
          reviewed, nothing found
        </div>
      )}
      {review.findings.length > 0 && (
        <ul data-testid={`dialog-filed-findings-${phaseId}`} className="flex flex-col text-body">
          {review.findings.map((finding) => (
            <li key={`${finding.repository}/${finding.path}:${finding.line}`}>
              <span className="font-mono">
                {finding.repository}/{finding.path}:{finding.line}
              </span>
              {` — ${finding.why}`}
              {finding.reverted && (
                <span
                  data-testid={`dialog-filed-reverted-${phaseId}`}
                  className="text-ink"
                >
                  {" — "}
                  {finding.reverted}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </>
  );
}
