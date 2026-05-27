"use client";

export function TrailTruncationBanner() {
  return (
    <div
      className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800"
      data-testid="trail-truncation-banner"
    >
      Trail starts mid-run. The oldest events were trimmed by Redis
      <span className="font-mono"> MAXLEN=10000</span> per the p0169e event-stream contract.
    </div>
  );
}
