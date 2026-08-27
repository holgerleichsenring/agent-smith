"use client";

import type { SpendSlice } from "@/hooks/useSpendBreakdown";
import { SectionHead } from "@/components/system/SectionHead";
import { SpendSlices } from "@/components/overview/SpendSlices";

// 2026-08-27-7463: where the trailing week's money went, grouped out of the run
// list the page already holds.
// 2026-08-27-559e: it is the wider of the two panels the Overview's second row
// carries, so it opens with its own header inside its own box rather than
// running the width of the page under the figures it breaks down.

export function SpendBreakdownPanel({
  slices,
  ready,
}: {
  slices: SpendSlice[];
  ready: boolean;
}) {
  return (
    <section className="ov-panel" data-testid="overview-spend">
      <SectionHead
        title="Where the money went"
        count={ready ? slices.length : undefined}
        sub="trailing 7 days · sums to the 7-day figure"
      />
      <div style={{ height: 14 }} />
      <SpendSlices slices={slices} ready={ready} />
    </section>
  );
}
