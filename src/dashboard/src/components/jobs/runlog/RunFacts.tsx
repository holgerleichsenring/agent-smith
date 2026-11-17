"use client";

import { useSandboxActivity } from "@/hooks/useSandboxActivity";
import type { RunSnapshot } from "@/types/hub-events";
import { budgetShare, computeOf, isTerminal, progressWords, timeSplitWords } from "./runFactWords";

// 2026-11-17-d6fbb: the rest of what the graph view's side rail showed, as text under the meta line — where the run
// is, where its time went, the sandbox's last command while it runs, why it has the shape it has, the budget it is
// spending against, and the pods behind a disclosure. A fact the run did not record draws nothing.

export function RunFacts({ snapshot }: { snapshot: RunSnapshot | null }) {
  const beat = useSandboxActivity(snapshot?.runId ?? null);
  if (!snapshot) return null;
  const live = !isTerminal(snapshot);
  const facts = [
    live ? progressWords(snapshot) : null,
    timeSplitWords(snapshot),
    live && beat ? `sandbox ${beat.commands} commands${beat.lastCommand ? `, last ${beat.lastCommand}` : ""}` : null,
  ].filter((f): f is string => f !== null);
  const budget = budgetShare(snapshot);
  const compute = computeOf(snapshot);
  if (facts.length === 0 && !budget && !compute && !snapshot.workShapeReason) return null;
  return (
    <div className="rl-facts" data-testid="run-facts">
      {facts.length > 0 && <div data-testid="run-facts-line">{facts.join(" · ")}</div>}
      {snapshot.workShapeReason && (
        <div data-testid="run-facts-shape">Shape {snapshot.workShape ?? "stated"}: {snapshot.workShapeReason}</div>
      )}
      {budget && (
        <div className="rl-budget" data-testid="run-facts-budget" data-tone={budget.tone}>
          <span className="rl-budget-bar" aria-hidden="true"><span style={{ width: `${budget.percent}%` }} /></span>
          <span>{budget.percent}% of the budget spent</span>
        </div>
      )}
      {compute && compute.pods.length > 0 && (
        <details className="rl-pods" data-testid="run-facts-pods">
          <summary>{compute.heading}</summary>
          {compute.pods.map((pod) => (
            <div key={pod.name} className="rl-line">
              <span className="rl-at">{pod.resources}</span>
              <span>{pod.name} <span className="rl-dim">{pod.image}</span></span>
            </div>
          ))}
        </details>
      )}
    </div>
  );
}
