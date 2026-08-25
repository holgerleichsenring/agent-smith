"use client";

import { usePathname } from "next/navigation";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { routeSurfaceName } from "@/lib/routeSurface";

// 2026-08-25-39ab: the ROUTE boundary. Next hands every throw below this segment
// here, so a run view that cannot render leaves the app rail, the degraded
// banner and every other route intact — the operator navigates away instead of
// reloading a blank tab. `reset` re-renders the segment, which is worth
// offering: most of these failures are one bad payload, not a broken build.

export default function RouteError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const surface = routeSurfaceName(usePathname());
  return (
    <div className="p-6" data-testid="route-error">
      <FailedSurface surface={surface} error={error} onRetry={reset} />
    </div>
  );
}
