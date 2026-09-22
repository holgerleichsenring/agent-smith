import { Suspense } from "react";

import { SpecDialogSurface } from "@/components/dialog/SpecDialogSurface";

// 2026-09-15-cb3e: a top-level destination, a peer of Runs and Pull requests — this is
// where the work is designed, not a diagnostic about the running system.
// 2026-09-21-f237b: behind a Suspense boundary, because the surface now reads ?open= to be
// handed a conversation by the conversations page. A search-param hook without a boundary
// fails the build for every statically rendered route, and this one is prerendered.
export default function SpecDialogPage() {
  return (
    <Suspense>
      <SpecDialogSurface />
    </Suspense>
  );
}
