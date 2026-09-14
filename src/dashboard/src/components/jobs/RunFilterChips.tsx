"use client";

import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "./runStatus";
import { Chip } from "@/components/ui/Chip";

// p0208: All/Running/Failed/Done filter chips with live counts over the merged
// run list. Selected chip filters the list. Client-side only — no new backend.

// p0320d: queued runs are filterable — they live in the active list but are
// waiting for capacity, not progressing.
// p0327: waiting_for_input runs are filterable too — parked on a question,
// waiting for the operator (the human is the bottleneck, surface it).
export type RunFilter = "all" | "run" | "queued" | "input" | "fail" | "ok";

const FILTERS: { key: RunFilter; label: string }[] = [
  { key: "all", label: "All" },
  { key: "run", label: "Running" },
  { key: "queued", label: "Queued" },
  { key: "input", label: "Waiting" },
  { key: "fail", label: "Failed" },
  { key: "ok", label: "Done" },
];

// p0439: the Done chip counts a shortfall too — it is a done, with its own glyph.
export function matchesFilter(status: string | null | undefined, filter: RunFilter): boolean {
  if (filter === "all") return true;
  const node = toNodeStatus(status);
  return node === filter || (filter === "ok" && node === "shortfall");
}

export function countByFilter(runs: RunSnapshot[], filter: RunFilter): number {
  return runs.filter((r) => matchesFilter(r.status, filter)).length;
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
