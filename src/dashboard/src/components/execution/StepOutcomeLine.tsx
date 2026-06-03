"use client";

// p0203: surfaces the handler's one-line outcome (Message) under the
// step row plus an optional per-repo aggregation summary ("4/5 ok, 1/5
// failed — repo-x"). Keeps ExecutionNode.tsx within the 120-line ceiling
// and isolates the styling so the visual treatment can evolve
// independently of the row layout.

interface RepoSummary {
  text: string;
  tone: "ok" | "warn" | "fail";
}

interface StepOutcomeLineProps {
  nodeId: string;
  indentPx: number;
  message: string | null;
  repoSummary: RepoSummary | null;
}

export function StepOutcomeLine({
  nodeId,
  indentPx,
  message,
  repoSummary,
}: StepOutcomeLineProps) {
  if (!message && !repoSummary) return null;
  return (
    <div
      data-testid={`step-outcome-${nodeId}`}
      className="border-t border-stone-100 bg-white py-1"
      style={{ paddingLeft: indentPx, paddingRight: 14 }}
    >
      {repoSummary && (
        <span
          data-testid={`step-outcome-${nodeId}-repos`}
          className={`mr-2 font-mono text-[11px] ${toneClass(repoSummary.tone)}`}
        >
          {repoSummary.text}
        </span>
      )}
      {message && (
        <span
          data-testid={`step-outcome-${nodeId}-message`}
          className="text-[12px] text-stone-700"
        >
          {message}
        </span>
      )}
    </div>
  );
}

function toneClass(tone: RepoSummary["tone"]): string {
  switch (tone) {
    case "ok":
      return "text-emerald-700";
    case "warn":
      return "text-amber-700";
    case "fail":
      return "text-rose-700";
  }
}
