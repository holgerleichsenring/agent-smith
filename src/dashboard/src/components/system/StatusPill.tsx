"use client";

import type { ProviderStatus } from "@/hooks/useSystemStatus";
import { Badge, type BadgeTone } from "@/components/ui/Badge";

const LABELS: Record<ProviderStatus, string> = {
  ok: "ok",
  degraded: "degraded",
  disconnected: "disconnected",
  unknown: "unknown",
};

// Green RESERVED for done — consistent with p0169j-d's topology palette.
const TONES: Record<ProviderStatus, BadgeTone> = {
  ok: "green",
  degraded: "amber",
  disconnected: "rose",
  unknown: "neutral",
};

export function StatusPill({ status }: { status: ProviderStatus }) {
  return (
    <Badge tone={TONES[status]} testId={`status-pill-${status}`}>
      {LABELS[status]}
    </Badge>
  );
}
