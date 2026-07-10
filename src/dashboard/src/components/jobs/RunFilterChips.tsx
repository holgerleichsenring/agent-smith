"use client";

import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "./runStatus";
import { Chip } from "@/components/ui/Chip";

// p0208: All/Running/Failed/Done filter chips with live counts over the merged
// run list. Selected chip filters the list. Client-side only — no new backend.

// p0320d: queued runs are filterable — they live in the active list but are
// waiting for capacity, not progressing.
export type RunFilter = "all" | "run" | "queued" | "fail" | "ok";

const FILTERS: { key: RunFilter; label: string }[] = [
  { key: "all", label: "All" },
  { key: "run", label: "Running" },
  { key: "queued", label: "Queued" },
  { key: "fail", label: "Failed" },
  { key: "ok", label: "Done" },
];

export function countByFilter(runs: RunSnapshot[], filter: RunFilter): number {
  if (filter === "all") return runs.length;
  return runs.filter((r) => toNodeStatus(r.status) === filter).length;
}

interface Props {
  runs: RunSnapshot[];
  active: RunFilter;
  onChange: (filter: RunFilter) => void;
}

export function RunFilterChips({ runs, active, onChange }: Props) {
  return (
    <div className="flex gap-2" data-testid="run-filter-chips">
      {FILTERS.map(({ key, label }) => (
        <Chip
          key={key}
          testId={`run-filter-${key}`}
          label={label}
          count={countByFilter(runs, key)}
          selected={key === active}
          onClick={() => onChange(key)}
        />
      ))}
    </div>
  );
}
