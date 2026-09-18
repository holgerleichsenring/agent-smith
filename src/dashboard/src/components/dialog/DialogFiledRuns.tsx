"use client";

import Link from "next/link";
import { runStatusWord } from "@/components/jobs/runStatus";
import type {
  FiledWorkFinding,
  FiledWorkHandback,
  FiledWorkPhase,
  FiledWorkPullRequest,
  FiledWorkReview,
  FiledWorkRun,
} from "@/types/spec-dialog";
import {
  isPhaseTerminal,
  markClass,
  phaseStatusWords,
  pullRequestWords,
  runStatusTone,
} from "./filedWorkStatus";

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
//
// 2026-09-17-042ef, found by looking at the rendered page: a run sits in a block one step deeper
// than the card that holds it, and three things the markup said but the page did not show are
// shown. A STATE is a mark, never a column value inline in prose — the five review states, the
// five phase statuses and the run's own each carry one, and the two that mean NOBODY LOOKED are
// the only ones an alarm tone, so a reader skimming the column sees the gap without reading it.
// A FINDING gets a row of its own with the address it rests on set above the prose, because two
// findings and a reverted fix pass arrived as one unbroken block and it is the densest and most
// consequential text on this page. A PULL REQUEST says that it is one.

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
    <div data-testid={`dialog-filed-run-${run.runId}`} className="d-block">
      <div className="flex flex-wrap items-baseline gap-x-2">
        <Link
          href={`/jobs/${encodeURIComponent(run.runId)}`}
          className="d-link font-mono font-semibold dsh-label"
        >
          {run.runId}
        </Link>
        <span className={markClass(runStatusTone(run.status))}>{runStatusWord(run.status)}</span>
        <span className="ec-sub">
          in {run.project} · ${run.costUsd.toFixed(2)}
        </span>
      </div>
      {run.pendingQuestion && (
        <p data-testid={`dialog-filed-waiting-${run.runId}`} className="mt-1 dsh-label text-ink">
          waiting for an answer: {run.pendingQuestion.text}
        </p>
      )}
      {run.pullRequests.length > 0 && (
        <div className="mt-1.5">
          {/* The list was a bare repository name beside the project the run is in, so nothing
              on the line said the second was a pull request rather than another repository the
              run had touched. The label says it once, for however many there are. */}
          <div className="fl">
            {run.pullRequests.length === 1 ? "pull request" : "pull requests"}
          </div>
          <ul className="ec-marks items-baseline">
            {run.pullRequests.map((pr) => (
              <PullRequest key={pr.repo} pr={pr} />
            ))}
          </ul>
        </div>
      )}
      {run.phases.length > 0 && (
        <ol className="mt-1.5 flex flex-col gap-1.5">
          {run.phases.map((phase) => (
            <Phase key={phase.phaseId} phase={phase} />
          ))}
        </ol>
      )}
    </div>
  );
}

/** One pull request: the repository it is against, linked where there is a link, its state in
 *  words, and — where it failed — the reason the row carries and nothing was showing.
 *  The link takes a glyph as well as a colour: it sits inline among repository names that are
 *  not links, and the colour alone is 1.35:1 against the ink beside it. */
function PullRequest({ pr }: { pr: FiledWorkPullRequest }) {
  const state = pullRequestWords(pr.status);
  return (
    <li className="flex flex-wrap items-baseline gap-x-1.5">
      {pr.url ? (
        <a href={pr.url} target="_blank" rel="noreferrer" className="d-link fv">
          {pr.repo}
          <span aria-hidden="true"> ↗</span>
        </a>
      ) : (
        <span className="fv">{pr.repo}</span>
      )}
      <span className={markClass(state.tone)}>{state.word}</span>
      {pr.reason && <span className="ec-sub">{pr.reason}</span>}
    </li>
  );
}

function Phase({ phase }: { phase: FiledWorkPhase }) {
  const status = phaseStatusWords(phase.status);
  return (
    <li data-testid={`dialog-filed-phase-${phase.phaseId}`} className="dsh-label">
      <div className="flex flex-wrap items-baseline gap-x-2">
        <span className="fv">{phase.phaseId}</span>
        <span className="text-body">{phase.title}</span>
        <span className={markClass(status.tone)}>{status.word}</span>
      </div>
      {phase.verdict && (
        <div data-testid={`dialog-filed-verdict-${phase.phaseId}`} className="ec-sub">
          {phase.verdict}
        </div>
      )}
      {phase.status === HANDED_BACK && (
        <div data-testid={`dialog-filed-amend-${phase.phaseId}`} className="text-ink">
          the specification is what has to change — approve the set again in this conversation.
        </div>
      )}
      <Review
        phaseId={phase.phaseId}
        review={phase.review}
        stopped={isPhaseTerminal(phase.status)}
      />
    </li>
  );
}

function Review({
  phaseId,
  review,
  stopped,
}: {
  phaseId: string;
  review: FiledWorkReview | null;
  /** Whether the phase has stopped: no review row on a stopped phase is a gap, not a not-yet. */
  stopped: boolean;
}) {
  // No row at all is a phase that never reached its review — the fourth state, and it says so
  // rather than staying silent, because silence is what a clean review would look like too.
  // It covers TWO facts, though, and one word for both put a phase that FINISHED with no review
  // beside "reviewed, nothing found" in the same calm grey. A phase still working has simply not
  // got there; a stopped one has a hole where its evidence should be, and that is the third of
  // the three gaps this page marks.
  if (review === null) {
    return (
      <div data-testid={`dialog-filed-noreview-${phaseId}`} className="ec-marks">
        <span className={stopped ? "ec-mark warn" : "ec-mark"}>
          {stopped ? "no review was recorded" : "not reviewed yet"}
        </span>
      </div>
    );
  }
  return (
    <>
      {/* The two states that mean NOBODY LOOKED are the two that carry an alarm mark. On screen
          their sentence read as more body text, which is precisely the clearance a review that
          was never taken must not be mistaken for. The sentence is unchanged; what it sits
          beside is not. */}
      {review.unreadable && (
        <div data-testid={`dialog-filed-unreadable-${phaseId}`} className="ec-marks items-baseline">
          <span className="ec-mark bad">unreadable</span>
          <span className="ec-sub">{review.why}</span>
        </div>
      )}
      {/* A report can say "not taken" AND still carry findings, so the note and the list are
          not alternatives; only a review that was taken and kept nothing is the quiet case. */}
      {!review.reviewed && !review.unreadable && (
        <div data-testid={`dialog-filed-unreviewed-${phaseId}`} className="ec-marks items-baseline">
          <span className="ec-mark warn">nobody looked</span>
          <span className="ec-sub">not reviewed: {review.why ?? "no reason was recorded"}</span>
        </div>
      )}
      {review.reviewed && review.findings.length === 0 && (
        <div data-testid={`dialog-filed-reviewed-${phaseId}`} className="ec-marks">
          <span className="ec-mark">reviewed, nothing found</span>
        </div>
      )}
      {review.findings.length > 0 && (
        <ul data-testid={`dialog-filed-findings-${phaseId}`} className="mt-1 flex flex-col">
          {review.findings.map((finding) => (
            <Finding key={address(finding)} finding={finding} phaseId={phaseId} />
          ))}
        </ul>
      )}
    </>
  );
}

/** Where a finding rests, which is what makes it unique among its phase's findings — and what
 *  its revert note is keyed by, because two reverted findings under one phase shared one id. */
function address(finding: FiledWorkFinding): string {
  return `${finding.repository}/${finding.path}:${finding.line}`;
}

/** One finding on a row of its own: the address it rests on, then what it says about the code. */
function Finding({ finding, phaseId }: { finding: FiledWorkFinding; phaseId: string }) {
  return (
    <li className="d-finding">
      <div className="fv">{address(finding)}</div>
      <div className="ec-sub">{finding.why}</div>
      {finding.reverted && (
        <div
          data-testid={`dialog-filed-reverted-${phaseId}-${address(finding)}`}
          className="ec-marks items-baseline"
        >
          <span className="ec-mark warn">reverted</span>
          <span className="ec-sub">{finding.reverted}</span>
        </div>
      )}
    </li>
  );
}
