"use client";

import { useCallback, useEffect, useState } from "react";
import { fetchRun } from "@/lib/runsApi";
import type { PendingQuestionInfo } from "@/types/hub-events";

// p0458 / 2026-10-13-9a59p: a parked run's question in its four honest states. A question the
// list already carries is used as is; without one, the run detail is fetched ONCE. An accepted
// answer is its own state — it used to be inferred from the question's absence, which is exactly
// what a successful answer produces. "unavailable" means only a run parked with nothing to answer.

export type ParkedQuestionState =
  | { kind: "loading" }
  | { kind: "ready"; question: PendingQuestionInfo }
  | { kind: "answered"; question: PendingQuestionInfo }
  | { kind: "unavailable" };

export interface ParkedQuestion {
  state: ParkedQuestionState;
  question: PendingQuestionInfo | null;
  onAnswered: () => void;
}

export function useParkedQuestion(runId: string, inline: PendingQuestionInfo | null): ParkedQuestion {
  const [state, setState] = useState<ParkedQuestionState>(
    inline ? { kind: "ready", question: inline } : { kind: "loading" },
  );

  useEffect(() => {
    if (state.kind !== "loading") return;
    let cancelled = false;
    const ctrl = new AbortController();
    fetchRun(runId, ctrl.signal)
      .then((detail) => {
        if (cancelled) return;
        setState(detail?.pendingQuestion ? { kind: "ready", question: detail.pendingQuestion } : { kind: "unavailable" });
      })
      .catch(() => {
        if (!cancelled) setState({ kind: "unavailable" });
      });
    return () => {
      cancelled = true;
      ctrl.abort();
    };
  }, [runId, state.kind]);

  // A question that reaches the list on a later read is adopted, unless it was answered here.
  useEffect(() => {
    if (!inline) return;
    setState((prev) => {
      if (prev.kind === "answered") return prev;
      if (prev.kind === "ready" && prev.question.questionId === inline.questionId) return prev;
      return { kind: "ready", question: inline };
    });
  }, [inline]);

  const onAnswered = useCallback(() => {
    setState((prev) => (prev.kind === "ready" ? { ...prev, kind: "answered" } : prev));
  }, []);

  const question = state.kind === "ready" || state.kind === "answered" ? state.question : null;
  return { state, question, onAnswered };
}
