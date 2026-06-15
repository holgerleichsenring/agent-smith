import { Badge } from "@/components/ui/Badge";
import type { ResolutionSource } from "@/lib/configApi";

// p0270b: provenance token for a resolved config value — WHERE the effective
// value comes from. An override (amber) is the operator's deviation from the
// shipped default (neutral); "per run" marks values only knowable at run time.

const PROVENANCE: Record<ResolutionSource, { tone: "neutral" | "amber"; label: string }> = {
  override: { tone: "amber", label: "override" },
  "global-default": { tone: "neutral", label: "default" },
  "run-resolved": { tone: "neutral", label: "per run" },
};

export function ProvenanceBadge({ source }: { source: ResolutionSource }) {
  const { tone, label } = PROVENANCE[source];
  return (
    <Badge tone={tone} testId={`provenance-${source}`}>
      {label}
    </Badge>
  );
}
