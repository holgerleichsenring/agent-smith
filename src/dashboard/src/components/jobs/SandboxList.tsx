"use client";

import { useMemo } from "react";
import type { RunEvent } from "@/types/hub-events";
import { EventType } from "@/types/hub-events";
import { SandboxBox } from "./SandboxBox";
import { ExpandCollapseControls } from "./ExpandCollapseControls";

interface Props {
  runId: string;
  events: RunEvent[];
  expanded: ReadonlySet<string>;
  onToggle: (repo: string) => void;
  onExpandAll: () => void;
  onCollapseAll: () => void;
}

export function SandboxList({ runId, events, expanded, onToggle, onExpandAll, onCollapseAll }: Props) {
  const repos = useMemo(() => extractSandboxRepos(events), [events]);
  if (repos.length === 0) {
    return <p className="text-sm text-stone-500" data-testid="sandbox-list-empty">No sandboxes yet.</p>;
  }
  return (
    <section className="space-y-2" data-testid="sandbox-list">
      <header className="flex items-center justify-between">
        <h2 className="text-sm font-medium text-stone-700">Sandboxes ({repos.length})</h2>
        <ExpandCollapseControls
          repoNames={repos}
          expanded={expanded}
          onExpandAll={onExpandAll}
          onCollapseAll={onCollapseAll}
        />
      </header>
      <div className="space-y-2">
        {repos.map((repo) => (
          <SandboxBox
            key={repo}
            runId={runId}
            repo={repo}
            expanded={expanded.has(repo)}
            onToggle={() => onToggle(repo)}
          />
        ))}
      </div>
    </section>
  );
}

function extractSandboxRepos(events: RunEvent[]): string[] {
  const repos = new Set<string>();
  for (const event of events) {
    if (event.type === EventType.SandboxCreated) repos.add(event.repo);
  }
  return [...repos].sort();
}
