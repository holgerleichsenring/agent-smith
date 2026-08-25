// 2026-08-25-e257: the client for the operator's judgements of a run's criterion verdicts.
//
// A run that fails on a wrong criterion looks exactly like one that fails on a right one —
// same state, same colour, same cost. Fourteen phases tuned the delivery account, each on a
// single failed run, because the only failures that announce themselves are mechanical. This
// is where the other kind gets written down.
//
// The account and its judgements arrive TOGETHER, from one endpoint: fetched apart there is
// a window in which the page shows a verdict whose correction has not landed yet, and the
// whole point of the correction is that the verdict on its own was misleading.

import { getJson, sendJson } from "@/lib/apiResponse";
import type { RunAcceptance } from "@/types/hub-events";

export type CriterionDisposition = "met" | "unmet" | "not_applicable" | "unproven";

export interface CriterionJudgement {
  criterion: string;
  machineStatus: CriterionDisposition;
  humanStatus: CriterionDisposition;
  reason: string;
  author: string;
  recordedAt: string;
}

export interface JudgedAcceptance {
  acceptance: RunAcceptance | null;
  judgements: CriterionJudgement[];
}

export async function fetchJudgedAcceptance(
  runId: string,
  signal?: AbortSignal,
): Promise<JudgedAcceptance> {
  return getJson<JudgedAcceptance>(`/api/runs/${encodeURIComponent(runId)}/acceptance`, signal);
}

/** Records one, replacing any earlier judgement of the same criterion. */
export async function recordJudgement(
  runId: string,
  judgement: {
    criterion: string;
    machineStatus: CriterionDisposition;
    humanStatus: CriterionDisposition;
    reason: string;
  },
  signal?: AbortSignal,
): Promise<JudgedAcceptance> {
  return sendJson<JudgedAcceptance>(
    "POST",
    `/api/runs/${encodeURIComponent(runId)}/judgements`,
    judgement,
    signal,
  );
}

/** Withdraws one. A judgement nobody stands behind any more must be removable, or the
 *  corpus records what nobody believes. */
export async function withdrawJudgement(
  runId: string,
  criterion: string,
  signal?: AbortSignal,
): Promise<JudgedAcceptance> {
  return sendJson<JudgedAcceptance>(
    "DELETE",
    `/api/runs/${encodeURIComponent(runId)}/judgements?criterion=${encodeURIComponent(criterion)}`,
    null,
    signal,
  );
}
