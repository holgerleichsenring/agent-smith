"use client";

import type { ProviderStatus } from "@/hooks/useSystemStatus";
import { Badge, type BadgeTone } from "@/components/ui/Badge";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

const LABELS: Record<ProviderStatus, TextKey> = {
  ok: "system.status.ok",
  degraded: "system.status.degraded",
  disconnected: "system.status.disconnected",
  unknown: "system.status.unknown",
};

// Green RESERVED for done — consistent with p0169j-d's topology palette.
const TONES: Record<ProviderStatus, BadgeTone> = {
  ok: "green",
  degraded: "amber",
  disconnected: "rose",
  unknown: "neutral",
};

export function StatusPill({ status }: { status: ProviderStatus }) {
  const t = useT();
  return (
    <Badge tone={TONES[status]} testId={`status-pill-${status}`}>
      {t(LABELS[status])}
    </Badge>
  );
}
