import type { ReactNode } from "react";

// 2026-11-19-1b72d: one headed part of an opened step — a small uppercase heading over hairline rows, no box. An
// opened spec step reads as four of them: Steps of the spec, Sub-agents, Build and tests, Activity.

export function StepPart({ title, testId, children }: { title: string; testId: string; children: ReactNode }) {
  return (
    <section className="rl-part" data-testid={testId} aria-label={title}>
      <h4 className="rl-lbl rl-part-head" data-testid="step-part-heading">{title}</h4>
      {children}
    </section>
  );
}
