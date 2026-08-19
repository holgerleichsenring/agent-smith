"use client";

import { shortRunId } from "@/lib/runId";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { fetchRun } from "@/lib/runsApi";
import type { PendingQuestionInfo, RunSnapshot } from "@/types/hub-events";
import { PendingQuestionCard } from "../PendingQuestionCard";
import { CancelRunButton } from "../CancelRunButton";
import { DeleteRunButton } from "../DeleteRunButton";
import { RunStats } from "../RunStats";

// p0343: a run parked on the operator (status="waiting_for_input"), answerable
// INLINE — the core "zero-navigation" promise of mission control. The run list
// carries the pendingQuestion when a checkpoint exists; when it does not, this
// card fetches the run detail to get it. Either way it reuses the existing
// PendingQuestionCard (which posts to /api/runs/{id}/answer and resumes the
// SAME run).
// p0343c (pixel identity): emits the runs-list.html .need DOM verbatim — .n-top
// (dot · ticket+title · activity line · waited) toggles the .n-body, which hosts
// the real question as a .q-item with quick-replies + free text, plus the
// cancel/delete/open affordances.

// p0458: the card's four honest states. An accepted answer is one of them —
// it used to be inferred from the ABSENCE of a question, which is exactly what
// a successful answer produces, so the operator who answered was told the
// question could not be loaded. "unavailable" now means only what it says: a
// run parked with nothing to answer (parked before p0453 checkpointed the
// master's mid-run question).
type QuestionState =
  | { kind: "loading" }
  | { kind: "ready"; question: PendingQuestionInfo }
  | { kind: "answered"; question: PendingQuestionInfo }
  | { kind: "unavailable" };

export function NeedsYouCard({ snapshot }: { snapshot: RunSnapshot }) {
  const inlineQuestion = snapshot.pendingQuestion ?? null;
  const [state, setState] = useState<QuestionState>(
    inlineQuestion ? { kind: "ready", question: inlineQuestion } : { kind: "loading" },
  );
  const [open, setOpen] = useState(true);

  // Only a card that has never seen its question fetches one. Once a question
  // is in hand the run may take its answer and drop the question — that is
  // progress, not a load failure, so nothing re-reads it.
  useEffect(() => {
    if (state.kind !== "loading") return;
    let cancelled = false;
    const ctrl = new AbortController();
    fetchRun(snapshot.runId, ctrl.signal)
      .then((detail) => {
        if (cancelled) return;
        setState(
          detail?.pendingQuestion
            ? { kind: "ready", question: detail.pendingQuestion }
            : { kind: "unavailable" },
        );
      })
      .catch(() => {
        if (!cancelled) setState({ kind: "unavailable" });
      });
    return () => {
      cancelled = true;
      ctrl.abort();
    };
  }, [snapshot.runId, state.kind]);

  // A question that only reaches the list on a later poll is not an unloadable
  // one — adopt it, unless the run has already been answered here.
  useEffect(() => {
    if (!inlineQuestion) return;
    setState((prev) => {
      if (prev.kind === "answered") return prev;
      if (prev.kind === "ready" && prev.question.questionId === inlineQuestion.questionId) return prev;
      return { kind: "ready", question: inlineQuestion };
    });
  }, [inlineQuestion]);

  const onAnswered = useCallback(() => {
    setState((prev) => (prev.kind === "ready" ? { ...prev, kind: "answered" } : prev));
  }, []);

  const href = `/jobs/${encodeURIComponent(snapshot.runId)}`;
  const question = state.kind === "ready" || state.kind === "answered" ? state.question : null;
  const waited = question ? waitedLabel(question.askedAt) : null;

  return (
    <div className="need" data-testid={`needs-you-${snapshot.runId}`}>
      <div
        className="n-top"
        role="button"
        tabIndex={0}
        onClick={() => setOpen((v) => !v)}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") setOpen((v) => !v);
        }}
        data-testid={`needs-you-${snapshot.runId}-toggle`}
      >
        <span className="sd" />
        <div className="rmain">
          <div className="rt">
            <span className="tick">
              {snapshot.ticketId ? `#${snapshot.ticketId}` : `#${shortRunId(snapshot.runId)}`}
            </span>
            {snapshot.ticketTitle && <span className="ttl">{snapshot.ticketTitle}</span>}
          </div>
          <div className="act">
            <span className="aq">1 question</span>
            {" · "}
            {snapshot.totalSteps > 0 ? (
              <>
                paused at <b>step {snapshot.stepIndex}/{snapshot.totalSteps}</b>
              </>
            ) : (
              <>paused on {snapshot.pipeline}</>
            )}
            {" · compute held, no tokens burning"}
          </div>
        </div>
        {/* p0445: the same four facts every other row states — spine, step
            position, cost, elapsed. A run that needs a decision is the one that
            must be readable without opening it. */}
        <RunStats snapshot={snapshot} progressTestId={`needs-you-${snapshot.runId}-progress`} />
        {waited && <span className="waited">waiting {waited}</span>}
      </div>

      {open && (
        <div className="n-body">
          {state.kind === "loading" && (
            <div className="qm" data-testid={`needs-you-${snapshot.runId}-loading`}>
              Loading the question…
            </div>
          )}
          {state.kind === "unavailable" && (
            <div className="qm" data-testid={`needs-you-${snapshot.runId}-unavailable`}>
              Question unavailable —{" "}
              <Link href={href} style={{ textDecoration: "underline" }}>
                open the run
              </Link>{" "}
              to answer.
            </div>
          )}
          {question && (
            <PendingQuestionCard
              runId={snapshot.runId}
              question={question}
              answered={state.kind === "answered"}
              onAnswered={onAnswered}
            />
          )}
          {/* The parked run stays fully actionable inline — cancel or delete it,
              or open the full story view, without leaving the home screen. */}
          <div className="n-answer" style={{ justifyContent: "flex-end" }}>
            <CancelRunButton runId={snapshot.runId} cancelRequested={snapshot.cancelRequested} />
            <DeleteRunButton runId={snapshot.runId} />
            <Link href={href} className="qm mono" data-testid={`needs-you-${snapshot.runId}-open`}>
              open ›
            </Link>
          </div>
        </div>
      )}
    </div>
  );
}

function waitedLabel(askedAtIso: string): string | null {
  const asked = new Date(askedAtIso).getTime();
  if (Number.isNaN(asked)) return null;
  const seconds = Math.max(0, Math.round((Date.now() - asked) / 1000));
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return `${minutes}m`;
  return `${Math.round(minutes / 60)}h`;
}
