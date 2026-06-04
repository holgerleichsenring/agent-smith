"use client";

import type { ProviderStatus } from "@/hooks/useSystemStatus";

const LABELS: Record<ProviderStatus, string> = {
  ok: "ok",
  degraded: "degraded",
  disconnected: "disconnected",
  unknown: "unknown",
};

const CLASSES: Record<ProviderStatus, string> = {
  // Green RESERVED for done — consistent with p0169j-d's topology palette.
  ok: "bg-emerald-100 text-emerald-700 border-emerald-300",
  degraded: "bg-amber-100 text-amber-700 border-amber-300",
  disconnected: "bg-rose-100 text-rose-700 border-rose-300",
  unknown: "bg-stone-100 text-stone-600 border-stone-300",
};

export function StatusPill({ status }: { status: ProviderStatus }) {
  return (
    <span
      className={`inline-flex items-center rounded-full border px-2 py-0.5 dsh-label font-medium ${CLASSES[status]}`}
      data-testid={`status-pill-${status}`}
    >
      {LABELS[status]}
    </span>
  );
}
