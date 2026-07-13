"use client";

import { Badge } from "@/components/ui/Badge";

// p0330: cancelRequested is durable state on the run row, rendered INDEPENDENT
// of status — a requested cancel stays visible across navigation and in every
// run state, not just while a local click is pending.
//   - not yet terminal            → amber "cancelling…" pill
//   - ended success/failed/error  → muted "cancel was requested" hint (the
//     enforced-late case: the run outran the cancel; honest, not alarming)
//   - status cancelled            → nothing (the status badge already says it)
const ENDED_UNCANCELLED = new Set(["success", "failed", "error"]);

interface Props {
  status: string;
  cancelRequested: boolean;
  className?: string;
}

export function CancelRequestedBadge({ status, cancelRequested, className }: Props) {
  if (!cancelRequested) return null;
  const s = status.toLowerCase();
  if (s === "cancelled") return null;
  if (ENDED_UNCANCELLED.has(s)) {
    return (
      <span
        data-testid="cancel-requested-hint"
        className={`dsh-label text-stone-400 ${className ?? ""}`}
      >
        cancel was requested
      </span>
    );
  }
  return (
    <Badge tone="amber" testId="cancel-requested-badge" className={className}>
      cancelling…
    </Badge>
  );
}
