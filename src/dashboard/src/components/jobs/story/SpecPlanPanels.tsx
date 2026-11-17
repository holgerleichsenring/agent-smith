"use client";

import { usePlanMarkdown } from "@/hooks/usePlanMarkdown";
import { useSpecMarkdown } from "@/hooks/useSpecMarkdown";
import { ResultDocument } from "@/components/jobs/ResultTab";

// 2026-11-17-d6fbb: the work spec and the agreed plan, drawn by the story's plan beat and by the run log's
// "Spec and plan" section alike.

// p0390: the work spec — WHAT this run must make true, and every revision with
// the cause that produced it. It sits ABOVE the plan because it is the statement
// of the work; the plan is how that work gets done. Absent for presets that
// derive no spec, and then the beat looks exactly as it did before.
export function SpecPanel({ runId }: { runId: string }) {
  const { content, loading } = useSpecMarkdown(runId);
  if (loading || !content) return null;
  return (
    <section className="card" data-testid="spec-panel">
      <div className="card-h">
        <h3>The work spec</h3>
      </div>
      <div className="card-b">
        <div data-testid="spec-markdown">
          <ResultDocument content={content} />
        </div>
      </div>
    </section>
  );
}

// The Plan beat's stage — the cached plan.md, in the mock's card chrome.
export function PlanPanel({ runId }: { runId: string }) {
  const { content, loading } = usePlanMarkdown(runId);
  return (
    <section className="card" data-testid="plan-panel">
      <div className="card-h">
        <h3>The agreed plan</h3>
      </div>
      <div className="card-b">
        {content ? (
          <div data-testid="plan-markdown">
            <ResultDocument content={content} />
          </div>
        ) : (
          <p className="hint">
            {loading
              ? "Loading…"
              : "No plan recorded for this run yet — the master writes plan.md during the run; it appears here once cached."}
          </p>
        )}
      </div>
    </section>
  );
}

