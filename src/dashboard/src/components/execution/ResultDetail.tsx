"use client";

import { ResultTab } from "@/components/jobs/ResultTab";

// p0205: the Result overview rendered in the detail pane. Wraps the existing
// p0169j-c ResultTab (cached result.md via react-markdown) as an Overview rail
// node so the run outcome lives beside the execution timeline, not stacked
// below it.

interface ResultDetailProps {
  runId: string;
  prUrl: string | null;
}

export function ResultDetail({ runId, prUrl }: ResultDetailProps) {
  return (
    <div data-testid="result-detail" className="h-full overflow-y-auto px-7 py-5">
      <div className="font-mono text-[11.5px] text-stone-400">Overview ›</div>
      <div className="text-[19px] font-semibold tracking-tight">Result</div>
      <div className="mt-4 border-t border-stone-100 pt-4">
        <ResultTab runId={runId} prUrl={prUrl} />
      </div>
    </div>
  );
}
