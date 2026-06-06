"use client";

import { useCallback, useState } from "react";
import type { RunEvent } from "@/types/hub-events";
import { TopologyGraph } from "@/components/jobs/TopologyGraph";
import { TopologyDetail } from "@/components/jobs/TopologyDetail";
import { useAnalyzeMarkdown } from "@/hooks/useAnalyzeMarkdown";
import { ResultDocument } from "@/components/jobs/ResultTab";

// p0205: the Architecture overview rendered in the detail pane. Wraps the
// existing p0169 TopologyGraph + TopologyDetail (the "what ran where" view,
// orthogonal to the execution timeline) and owns the selected-sandbox state.

interface ArchitectureDetailProps {
  runId: string;
  pipeline: string | null;
  events: readonly RunEvent[];
  repoCount: number;
}

export function ArchitectureDetail({ runId, pipeline, events, repoCount }: ArchitectureDetailProps) {
  const [selectedRepo, setSelectedRepo] = useState<string | null>(null);
  const selectRepo = useCallback((repo: string) => {
    setSelectedRepo((prev) => (prev === repo ? null : repo));
  }, []);
  // p0243: the analyzer's output (what the agent understood), cached right after
  // the Analyze step — visible LIVE mid-run so the operator can judge whether the
  // analysis is sensible before the agent acts on it (and cancel if not).
  const { content: analyze } = useAnalyzeMarkdown(runId);

  return (
    <div data-testid="architecture-detail" className="h-full overflow-y-auto px-7 py-5">
      <div className="font-mono dsh-mono text-stone-400">Overview ›</div>
      <div className="dsh-h2 font-semibold tracking-tight">Architecture</div>
      <p className="mt-2 max-w-2xl text-sm text-stone-500">
        {repoCount} {repoCount === 1 ? "repository" : "repositories"} — the what-ran-where view,
        orthogonal to the execution timeline on the left.
      </p>

      {analyze && (
        <section className="mt-4 space-y-2 border-t border-stone-100 pt-4" data-testid="analyze-section">
          <span className="text-xs text-stone-500">analyze.md — what the agent understood</span>
          <article
            className="max-w-none rounded-lg border border-stone-200 bg-white p-6"
            data-testid="analyze-markdown"
          >
            <ResultDocument content={analyze} />
          </article>
        </section>
      )}

      <div className="mt-4 space-y-4 border-t border-stone-100 pt-4">
        <TopologyGraph
          pipeline={pipeline}
          runId={runId}
          events={events}
          selected={selectedRepo}
          onSelect={selectRepo}
        />
        <TopologyDetail runId={runId} selected={selectedRepo} />
      </div>
    </div>
  );
}
