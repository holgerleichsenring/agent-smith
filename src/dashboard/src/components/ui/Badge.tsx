"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0219: shared metadata/status pill over the DESIGN.md badge-pill shape. Tone
// maps to the status palette (green reserved for done, per the topology
// palette); the base shape + spacing come from the badge-pill class.

export type BadgeTone = "neutral" | "green" | "amber" | "rose";

interface BadgeProps {
  tone?: BadgeTone;
  children: ReactNode;
  className?: string;
  testId?: string;
}

const TONES: Record<BadgeTone, string> = {
  neutral: "bg-stone-100 text-stone-600 border-stone-300",
  green: "bg-emerald-100 text-emerald-700 border-emerald-300",
  amber: "bg-amber-100 text-amber-700 border-amber-300",
  rose: "bg-rose-100 text-rose-700 border-rose-300",
};

export function Badge({ tone = "neutral", children, className, testId }: BadgeProps) {
  return (
    <span
      data-testid={testId}
      className={cn("badge-pill border dsh-label font-medium", TONES[tone], className)}
    >
      {children}
    </span>
  );
}
