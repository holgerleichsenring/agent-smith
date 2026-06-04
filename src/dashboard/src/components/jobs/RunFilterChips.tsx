"use client";

import type { RunSnapshot } from "@/types/hub-events";
import { toNodeStatus } from "./runStatus";

// p0208: All/Running/Failed/Done filter chips with live counts over the merged
// run list. Selected chip filters the list. Client-side only — no new backend.

export type RunFilter = "all" | "run" | "fail" | "ok";

const FILTERS: { key: RunFilter; label: string }[] = [
  { key: "all", label: "All" },
  { key: "run", label: "Running" },
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
      {FILTERS.map(({ key, label }) => {
        const on = key === active;
        return (
          <button
            key={key}
            type="button"
            data-testid={`run-filter-${key}`}
            data-active={on ? "true" : "false"}
            onClick={() => onChange(key)}
            className={`select-none rounded-full border px-3 py-1 dsh-body transition ${
              on
                ? "border-stone-900 bg-stone-900 text-white"
                : "border-stone-200 bg-white text-stone-500 hover:border-stone-300"
            }`}
          >
            {label}
            <span className={`ml-1.5 ${on ? "text-white/60" : "text-stone-400"}`}>
              {countByFilter(runs, key)}
            </span>
          </button>
        );
      })}
    </div>
  );
}
