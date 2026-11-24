"use client";

import { useT } from "@/hooks/useT";
import type { SeriesMergeOutcomeState } from "./useSeriesMerge";

// 2026-11-23-31dce: what a merge run did to each repo's pull request, or why it refused. 2026-11-23-f562: drawn once, at the
// top of the Overview.

export function SeriesMergeOutcome({ outcome }: { outcome: SeriesMergeOutcomeState }) {
  const t = useT();
  if ("failed" in outcome)
    return <div className="sr-outcome bad" role="status">{t("series.merge.failed", { message: outcome.failed })}</div>;
  if (outcome.result.kind === "said")
    return <div className="sr-outcome bad" role="status" data-testid="series-merge-said">{outcome.result.text}</div>;
  return (
    <div className="sr-outcome" role="status" data-testid="series-merge-outcome">
      {outcome.result.answer.pullRequests.map((pr) => (
        <div key={pr.repo} data-outcome={pr.outcome}>
          <span className={`sr-state ${pr.outcome === "merged" ? "ok" : pr.outcome === "armed" ? "run" : "bad"}`}>
            {t(`series.merge.outcome.${pr.outcome}`)}
          </span>{" "}{pr.outcome === "merged" ? pr.repo : pr.text}
        </div>
      ))}
    </div>
  );
}
