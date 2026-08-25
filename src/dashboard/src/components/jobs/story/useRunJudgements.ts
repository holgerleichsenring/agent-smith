"use client";

import { useCallback, useEffect, useState } from "react";
import {
  fetchJudgedAcceptance,
  recordJudgement,
  withdrawJudgement,
  type CriterionDisposition,
  type CriterionJudgement,
} from "@/lib/judgementsApi";

/**
 * 2026-08-25-e257: the run's judgements, and the two ways to change them.
 *
 * Fetched on its own rather than riding the run snapshot: the snapshot is pushed on every
 * lifecycle event and a judgement changes only when a person changes it. Both writes return
 * the whole set, so the page never has to guess what the server now holds.
 *
 * A failed write leaves the previous set standing and reports the error — a label that
 * silently did not land is worse than a visible refusal, because the operator would believe
 * it was recorded.
 */
export function useRunJudgements(runId: string, enabled: boolean) {
  const [judgements, setJudgements] = useState<CriterionJudgement[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    fetchJudgedAcceptance(runId, controller.signal)
      .then((served) => setJudgements(served.judgements ?? []))
      .catch(() => {
        // A run whose judgements cannot be read still shows its verdicts; the control
        // simply has nothing recorded to display.
      });
    return () => controller.abort();
  }, [runId, enabled]);

  const record = useCallback(
    (
      criterion: string,
      machineStatus: CriterionDisposition,
      humanStatus: CriterionDisposition,
      reason: string,
    ) => {
      setBusy(true);
      setError(null);
      recordJudgement(runId, { criterion, machineStatus, humanStatus, reason })
        .then((served) => setJudgements(served.judgements ?? []))
        .catch((cause: unknown) =>
          setError(cause instanceof Error ? cause.message : String(cause)))
        .finally(() => setBusy(false));
    },
    [runId],
  );

  const withdraw = useCallback(
    (criterion: string) => {
      setBusy(true);
      setError(null);
      withdrawJudgement(runId, criterion)
        .then((served) => setJudgements(served.judgements ?? []))
        .catch((cause: unknown) =>
          setError(cause instanceof Error ? cause.message : String(cause)))
        .finally(() => setBusy(false));
    },
    [runId],
  );

  return { judgements, busy, error, record, withdraw };
}
