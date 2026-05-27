"use client";

import type { GateChip } from "@/types/trail-node";

interface Props {
  chips: GateChip[];
}

export function StepGateChips({ chips }: Props) {
  if (chips.length === 0) return null;
  return (
    <span className="flex items-center gap-1" data-testid="gate-chips">
      {chips.map((chip, idx) => (
        <span
          key={`${chip.gate}-${idx}`}
          title={chip.reason}
          className={`rounded-full px-1.5 py-0.5 text-[10px] ${
            chip.passed
              ? "bg-emerald-50 text-emerald-700"
              : "bg-rose-50 text-rose-700"
          }`}
        >
          {chip.gate}: {chip.passed ? "✓" : "✗"}
        </span>
      ))}
    </span>
  );
}
