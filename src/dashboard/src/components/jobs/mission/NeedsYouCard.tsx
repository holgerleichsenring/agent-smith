"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { fetchRun } from "@/lib/runsApi";
import type { PendingQuestionInfo, RunSnapshot } from "@/types/hub-events";
import { PendingQuestionCard } from "../PendingQuestionCard";
import { CancelRunButton } from "../CancelRunButton";
import { DeleteRunButton } from "../DeleteRunButton";

// p0343: a run parked on the operator (status="waiting_for_input"), answerable
// INLINE — the core "zero-navigation" promise of mission control. The overview
// list does not carry the pendingQuestion (REST-detail only), so this card
// fetches the run detail to get it, then reuses the existing PendingQuestionCard
// (which posts to /api/runs/{id}/answer and resumes the SAME run).
// p0343c (pixel identity): emits the runs-list.html .need DOM verbatim — .n-top
// (dot · ticket+title · activity line · waited) toggles the .n-body, which hosts
// the real question as a .q-item with quick-replies + free text, plus the
// cancel/delete/open affordances.

type LoadState = "idle" | "loading" | "unavailable";

export function NeedsYouCard({ snapshot }: { snapshot: RunSnapshot }) {
  const inlineQuestion = snapshot.pendingQuestion ?? null;
  const [question, setQuestion] = useState<PendingQuestionInfo | null>(inlineQuestion);
  const [state, setState] = useState<LoadState>(inlineQuestion ? "idle" : "loading");
  const [open, setOpen] = useState(true);

  useEffect(() => {
    if (inlineQuestion) return;
    let cancelled = false;
    const ctrl = new AbortController();
    setState("loading");
    fetchRun(snapshot.runId, ctrl.signal)
      .then((detail) => {
        if (cancelled) return;
        if (detail?.pendingQuestion) {
          setQuestion(detail.pendingQuestion);
          setState("idle");
        } else {
          setState("unavailable");
        }
      })
      .catch(() => {
        if (!cancelled) setState("unavailable");
      });
    return () => {
      cancelled = true;
      ctrl.abort();
    };
  }, [snapshot.runId, inlineQuestion]);

  const href = `/jobs/${encodeURIComponent(snapshot.runId)}`;
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
              {snapshot.ticketId ? `#${snapshot.ticketId}` : `#${snapshot.runId.slice(0, 8)}`}
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
        {waited && <span className="waited">waiting {waited}</span>}
      </div>

      {open && (
        <div className="n-body">
          {state === "loading" && (
            <div className="qm" data-testid={`needs-you-${snapshot.runId}-loading`}>
              Loading the question…
            </div>
          )}
          {state === "unavailable" && (
            <div className="qm">
              Question unavailable —{" "}
              <Link href={href} style={{ textDecoration: "underline" }}>
                open the run
              </Link>{" "}
              to answer.
            </div>
          )}
          {question && <PendingQuestionCard runId={snapshot.runId} question={question} />}
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
