"use client";

import type { DeliveryPeriod } from "@/lib/deliveryApi";

// 2026-10-25-0099c: the period switch and the project chips. The chips are the projects the server says the caller
// may read; every chip is on at first, and the last one on cannot be switched off.

const PERIODS: ReadonlyArray<{ id: DeliveryPeriod; label: string }> = [
  { id: "day", label: "Day" },
  { id: "week", label: "Week" },
  { id: "month", label: "Month" },
];

export function DeliveryPeriodSwitch({ period, onPeriod }: { period: DeliveryPeriod; onPeriod: (p: DeliveryPeriod) => void }) {
  return (
    <div className="seg" role="group" aria-label="Period">
      {PERIODS.map((p) => (
        <button key={p.id} type="button" aria-pressed={p.id === period} data-testid={`delivery-period-${p.id}`} onClick={() => onPeriod(p.id)}>
          {p.label}
        </button>
      ))}
    </div>
  );
}

export function DeliveryChips({
  projects, off, onToggle,
}: { projects: readonly string[]; off: ReadonlySet<string>; onToggle: (project: string) => void }) {
  const on = projects.filter((p) => !off.has(p));
  return (
    <div className="scope">
      <div className="chips" role="group" aria-label="Projects">
        {projects.map((p) => (
          <button
            key={p} type="button" className="chip" aria-pressed={!off.has(p)} data-testid={`delivery-chip-${p}`}
            disabled={on.length === 1 && on[0] === p}
            onClick={() => onToggle(p)}
          >
            {p}
          </button>
        ))}
      </div>
      <span className="rights">Your projects</span>
    </div>
  );
}
