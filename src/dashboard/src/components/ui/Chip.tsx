"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0219: the single filter-chip primitive. Replaces the three near-identical
// implementations (RunFilterChips, ActivityPills, EventDrawer filters) so the
// selected / count / pressed states are defined exactly once.

interface ChipProps {
  label: ReactNode;
  selected: boolean;
  onClick: () => void;
  /** Optional trailing count (e.g. the run-filter buckets). */
  count?: number;
  testId?: string;
  /** Defaults to `selected`; override when the chip is a toggle whose pressed
      state differs from its visual selection. */
  ariaPressed?: boolean;
  ariaLabel?: string;
}

export function Chip({ label, selected, onClick, count, testId, ariaPressed, ariaLabel }: ChipProps) {
  return (
    <button
      type="button"
      data-testid={testId}
      data-active={selected}
      aria-pressed={ariaPressed ?? selected}
      aria-label={ariaLabel}
      onClick={onClick}
      className={cn(
        "select-none rounded-full border px-3 py-1 dsh-body transition",
        selected
          ? "border-stone-900 bg-stone-900 text-white"
          : "border-stone-200 bg-white text-stone-500 hover:border-stone-300",
      )}
    >
      {label}
      {count !== undefined && (
        <span
          data-testid="chip-count"
          className={cn("ml-1.5", selected ? "text-white/60" : "text-stone-400")}
        >
          {count}
        </span>
      )}
    </button>
  );
}
