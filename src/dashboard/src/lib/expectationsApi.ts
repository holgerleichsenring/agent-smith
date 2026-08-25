// p0329: client for the expectation-metrics read surface — the p0328
// ratification outcomes aggregated per project into the two headline rates.
// expectationHitRate = verbatim / human-ratified (null before any human
// ratification); firstPrAcceptance = (verbatim+edited) / all negotiated runs.

import { getJson } from "@/lib/apiResponse";

export interface OutcomeCounts {
  total: number;
  verbatim: number;
  edited: number;
  rejected: number;
  unratified: number;
}

export interface MonthMetrics {
  month: string;
  counts: OutcomeCounts;
}

export interface ProjectExpectationMetrics {
  project: string;
  counts: OutcomeCounts;
  expectationHitRate: number | null;
  firstPrAcceptance: number;
  averageEditDistance: number | null;
  months: MonthMetrics[];
}

export interface ExpectationMetrics {
  total: number;
  projects: ProjectExpectationMetrics[];
}

export async function fetchExpectationMetrics(signal?: AbortSignal): Promise<ExpectationMetrics> {
  return getJson<ExpectationMetrics>(`/api/runs/expectations/metrics`, signal);
}
