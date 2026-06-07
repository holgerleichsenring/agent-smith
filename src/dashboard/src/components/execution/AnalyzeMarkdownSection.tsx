"use client";

import { useAnalyzeMarkdown } from "@/hooks/useAnalyzeMarkdown";
import { ResultDocument } from "@/components/jobs/ResultTab";

// p0247: the analyzer's output ("what the agent understood"), cached right after
// the Analyze step (p0243). Shared by the Architecture overview node AND the
// Analyze-codebase execution step's detail pane, so the operator finds it where
// they look for it. Renders nothing until analyze.md is available.
export function AnalyzeMarkdownSection({ runId }: { runId: string }) {
  const { content: analyze } = useAnalyzeMarkdown(runId);
  if (!analyze) return null;
  return (
    <section className="mt-4 space-y-2 border-t border-stone-100 pt-4" data-testid="analyze-section">
      <span className="text-xs text-stone-500">analyze.md — what the agent understood</span>
      <article
        className="max-w-none rounded-lg border border-stone-200 bg-white p-6"
        data-testid="analyze-markdown"
      >
        <ResultDocument content={analyze} />
      </article>
    </section>
  );
}
