// The Criteria met read surface: the criteria finished coding runs were judged on,
// counted by the status they ended on after the operator's overrules — overall, per
// project, and per project per month of the run's finish (UTC). The route keeps its
// expectation-era name.
//
// share = met / judged, judged = met + unmet + unproven; not_applicable is in neither.
// An overrule counts only while the status it answered is still the snapshot's;
// otherwise it is stale, counted in staleOverrules and not applied. share is null
// where nothing was judged.

import { getJson } from "@/lib/apiResponse";

export interface CriterionCounts {
  met: number;
  unmet: number;
  unproven: number;
  notApplicable: number;
  overruled: number;
  staleOverrules: number;
  judged: number;
  share: number | null;
}

export interface MonthCriteria {
  month: string;
  runs: number;
  counts: CriterionCounts;
}

export interface ProjectCriteria {
  project: string;
  runs: number;
  counts: CriterionCounts;
  months: MonthCriteria[];
}

export interface CriteriaMet {
  runs: number;
  counts: CriterionCounts;
  projects: ProjectCriteria[];
}

export async function fetchExpectationMetrics(signal?: AbortSignal): Promise<CriteriaMet> {
  return getJson<CriteriaMet>(`/api/runs/expectations/metrics`, signal);
}
