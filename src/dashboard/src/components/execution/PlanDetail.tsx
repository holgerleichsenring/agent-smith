"use client";

import { usePlanMarkdown } from "@/hooks/usePlanMarkdown";
import { ResultDocument } from "@/components/jobs/ResultTab";

// p0258: a dedicated overview node for the agent's plan, placed right AFTER the
// Architecture node (analyze.md = "what the agent understood") so the operator
// reads "what it understood" → "what it intends to do" in sequence. plan.md is
// cached at run-finish (p0235); shows a hint until it is available.
export function PlanDetail({ runId }: { runId: string }) {
  const { content, loading } = usePlanMarkdown(runId);
  return (
    <div data-testid="plan-detail" className="content-shell h-full overflow-y-auto">
      <div className="breadcrumb">Overview ›</div>
      <div className="dsh-h2 font-semibold tracking-tight">Plan</div>
      <p className="mt-1 text-xs text-stone-500">plan.md — what the agent intends to do</p>
      <div className="mt-4 border-t border-stone-100 pt-4">
        {content ? (
          <article
            className="max-w-none rounded-lg border border-stone-200 bg-white p-6"
            data-testid="plan-markdown"
          >
            <ResultDocument content={content} />
          </article>
        ) : (
          <div className="text-sm text-stone-400">
            {loading
              ? "Loading…"
              : "No plan recorded for this run yet — the master writes plan.md during the run; it appears here once cached."}
          </div>
        )}
      </div>
    </div>
  );
}
