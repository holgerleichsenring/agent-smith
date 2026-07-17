"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0345: small shared presentational atoms for the studio — a labelled
// field-block (eyebrow label over a value) and a wiring chip (an arrow-linked
// reference pill that goes rose when the referenced entity is missing).

export function FieldBlock({
  label,
  children,
  testId,
}: {
  label: string;
  children: ReactNode;
  testId?: string;
}) {
  return (
    <div data-testid={testId} className="min-w-0">
      <div className="eyebrow-uppercase text-stone-400">{label}</div>
      <div className="dsh-body truncate text-stone-700">{children}</div>
    </div>
  );
}

// A resolved reference. `resolved=false` means the pointed-at entity is gone —
// rendered rose so a dangling ref is impossible to miss.
export function WiringChip({
  label,
  value,
  resolved,
  testId,
}: {
  label: string;
  value: string;
  resolved: boolean;
  testId?: string;
}) {
  return (
    <span
      data-testid={testId}
      data-resolved={resolved ? "true" : "false"}
      className={cn(
        "badge-pill border dsh-label font-mono gap-1",
        resolved
          ? "bg-stone-100 text-stone-600 border-stone-300"
          : "bg-rose-100 text-rose-700 border-rose-300",
      )}
    >
      <span className="text-stone-400">{label}</span>
      {value || "—"}
    </span>
  );
}
