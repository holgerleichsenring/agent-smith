// p0329: client for the expectation-metrics read surface — the p0328
// ratification outcomes aggregated per project into the two headline rates.
// expectationHitRate = verbatim / human-ratified (null before any human
// ratification); firstPrAcceptance = (verbatim+edited) / all negotiated runs.

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

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
  const res = await fetch(`${API_BASE}/api/runs/expectations/metrics`, { signal });
  if (!res.ok) throw new Error(`HTTP ${res.status}`);
  return (await res.json()) as ExpectationMetrics;
}
