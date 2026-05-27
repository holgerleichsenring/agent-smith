"use client";

import { SandboxBox } from "./SandboxBox";

interface Props {
  runId: string;
  selected: string | null;
}

// p0169j-d: detail pane below the topology graph. When a sandbox node
// is selected, reuses SandboxBox in always-expanded mode scoped to
// that runId + repo. When nothing is selected, empty-state copy.

export function TopologyDetail({ runId, selected }: Props) {
  if (selected === null) {
    return (
      <div
        className="rounded-lg border border-dashed border-stone-300 bg-white p-6 text-sm text-stone-500"
        data-testid="topology-detail-empty"
      >
        Select a sandbox to see its stdout/stderr.
      </div>
    );
  }
  return (
    <div data-testid="topology-detail">
      <SandboxBox
        runId={runId}
        repo={selected}
        expanded
        onToggle={() => {
          /* expansion is controlled by graph selection — no-op here */
        }}
      />
    </div>
  );
}
