import type { ResolutionSource } from "@/lib/configApi";

// p0271: provenance is now a SUBTLE muted inline hint, not a coloured pill — the
// operator wants the effective value foremost, with "how it came to be" available
// but quiet. "override" = operator deviation; "default" = shipped default;
// "per run" = only knowable at run time.

const LABELS: Record<ResolutionSource, string> = {
  override: "override",
  "global-default": "default",
  "run-resolved": "per run",
};

export function ProvenanceBadge({ source }: { source: ResolutionSource }) {
  return (
    <span className="dsh-label text-stone-400" data-testid={`provenance-${source}`}>
      {LABELS[source]}
    </span>
  );
}
