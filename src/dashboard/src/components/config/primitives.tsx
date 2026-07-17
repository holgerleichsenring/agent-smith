"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0345/p0343c: shared presentational atoms for the studio, emitting the
// config-studio.html mock DOM — the .fields/.f field-block (used via FieldBlock
// inside a .fields row) and the .wchip wiring chip whose dot color encodes the
// referenced kind. A dangling ref renders via data-resolved="false" (rose).

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
    <div className="f" data-testid={testId}>
      <span className="fl">{label}</span>
      <span className="fv">{children}</span>
    </div>
  );
}

// A resolved reference. `resolved=false` means the pointed-at entity is gone —
// rendered rose so a dangling ref is impossible to miss. `kind` picks the
// mock's dot color (agent=green, tracker=ok, repo=amber).
export function WiringChip({
  label,
  value,
  resolved,
  kind,
  testId,
}: {
  label: string;
  value: string;
  resolved: boolean;
  kind?: "agent" | "tracker" | "repo";
  testId?: string;
}) {
  return (
    <span
      data-testid={testId}
      data-resolved={resolved ? "true" : "false"}
      className={cn("wchip", kind)}
      title={label}
    >
      <span className="wd" />
      {value || "—"}
    </span>
  );
}
