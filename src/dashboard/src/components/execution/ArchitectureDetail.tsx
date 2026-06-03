"use client";

import { useCallback, useState } from "react";
import type { RunEvent } from "@/types/hub-events";
import { TopologyGraph } from "@/components/jobs/TopologyGraph";
import { TopologyDetail } from "@/components/jobs/TopologyDetail";

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

  return (
    <div data-testid="architecture-detail" className="max-h-[80vh] overflow-y-auto px-7 py-5">
      <div className="font-mono text-[11.5px] text-stone-400">Overview ›</div>
      <div className="text-[19px] font-semibold tracking-tight">Architecture</div>
      <p className="mt-2 max-w-2xl text-sm text-stone-500">
        {repoCount} {repoCount === 1 ? "repository" : "repositories"} — the what-ran-where view,
        orthogonal to the execution timeline on the left.
      </p>
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
