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
// (which posts to /api/runs/{id}/answer and resumes the SAME run). The operator
// never leaves the home screen to unblock a run.

type LoadState = "idle" | "loading" | "unavailable";

export function NeedsYouCard({ snapshot }: { snapshot: RunSnapshot }) {
  const inlineQuestion = snapshot.pendingQuestion ?? null;
  const [question, setQuestion] = useState<PendingQuestionInfo | null>(inlineQuestion);
  const [state, setState] = useState<LoadState>(inlineQuestion ? "idle" : "loading");

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

  return (
    <div
      data-testid={`needs-you-${snapshot.runId}`}
      className="rounded-md border border-l-[3px] border-violet-200 border-l-violet-400 bg-white p-4"
    >
      <div className="flex items-start gap-3">
        <div className="min-w-0 flex-1">
          {snapshot.ticketTitle && (
            <div className="truncate dsh-h3 font-semibold text-stone-900">{snapshot.ticketTitle}</div>
          )}
          <div className="mt-0.5 dsh-body text-stone-500">
            {snapshot.ticketId && (
              <code className="mr-1.5 rounded bg-stone-100 px-1.5 py-0.5 font-mono dsh-mono text-stone-600">
                #{snapshot.ticketId}
              </code>
            )}
            <span>{snapshot.pipeline}</span>
            {snapshot.totalSteps > 0 && (
              <span>
                <span className="mx-1.5 text-stone-300">·</span>
                paused at step {snapshot.stepIndex}/{snapshot.totalSteps}
              </span>
            )}
            <span className="mx-1.5 text-stone-300">·</span>
            compute held, no tokens burning
          </div>
        </div>
        {/* A parked run stays fully actionable inline — answer, or cancel /
            delete it without opening the detail page. */}
        <div className="flex flex-none items-center gap-2">
          <CancelRunButton runId={snapshot.runId} cancelRequested={snapshot.cancelRequested} />
          <DeleteRunButton runId={snapshot.runId} />
          <Link
            href={href}
            data-testid={`needs-you-${snapshot.runId}-open`}
            className="font-mono dsh-mono text-stone-400 transition hover:text-stone-700"
          >
            open ›
          </Link>
        </div>
      </div>

      {state === "loading" && (
        <div data-testid={`needs-you-${snapshot.runId}-loading`} className="mt-3 dsh-body text-stone-400">
          Loading the question…
        </div>
      )}
      {state === "unavailable" && (
        <div className="mt-3 dsh-body text-stone-400">
          Question unavailable —{" "}
          <Link href={href} className="underline hover:text-stone-600">
            open the run
          </Link>{" "}
          to answer.
        </div>
      )}
      {question && <PendingQuestionCard runId={snapshot.runId} question={question} />}
    </div>
  );
}
