"use client";

import type { ResolutionSource } from "@/lib/configApi";
import { useT } from "@/hooks/useT";
import type { TextKey } from "@/lib/i18n/translate";

// p0271: provenance is now a SUBTLE muted inline hint, not a coloured pill — the
// operator wants the effective value foremost, with "how it came to be" available
// but quiet. "override" = operator deviation; "default" = shipped default;
// "per run" = only knowable at run time.

const LABELS: Record<ResolutionSource, TextKey> = {
  override: "system.provenance.override",
  "global-default": "system.provenance.default",
  "run-resolved": "system.provenance.perRun",
  // 2026-09-22-6c46: a table in the code answered, not a setting — saying "default" here
  // would send an operator looking for a configuration key that does not exist.
  "code-default": "system.provenance.codeDefault",
  // 2026-09-23-2446: an environment variable answered, not a setting either — and this one
  // is not even in the code, so neither "default" nor "code default" names where to look.
  "environment-variable": "system.provenance.environment",
};

export function ProvenanceBadge({ source }: { source: ResolutionSource }) {
  const t = useT();
  return (
    <span className="dsh-label text-stone-400" data-testid={`provenance-${source}`}>
      {t(LABELS[source])}
    </span>
  );
}
