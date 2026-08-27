"use client";

import type { SpendSlice } from "@/hooks/useSpendBreakdown";

// 2026-08-27-559e: a run over several repositories names every one of them on
// its work line, and that line used to set the row's width. It is truncated
// with the full set kept on the element's title, because a breakdown row exists
// to be compared against its neighbours and a row stretched to fit its label
// makes every proportion bar incomparable.
// 2026-08-27-7463: the breakdown rows — one line per repo-set and pipeline, the
// amount, and a bar drawn at the slice's share of the total. The section keeps
// its own loading and empty states: the run list is one of three answers this
// page waits on, and a missing one costs its own box, not the page.

export function SpendSlices({ slices, ready }: { slices: SpendSlice[]; ready: boolean }) {
  if (!ready) {
    return (
      <div className="stateline" data-testid="overview-spend-loading">
        Reading the run ledger…
      </div>
    );
  }
  if (slices.length === 0) {
    return (
      <div className="empty" data-testid="overview-spend-empty">
        <div className="ei" aria-hidden>
          ◍
        </div>
        No LLM spend recorded in the last 7 days.
      </div>
    );
  }
  return (
    <div className="rows" data-testid="overview-spend-rows">
      {slices.map((slice) => (
        <SpendRow key={slice.key} slice={slice} />
      ))}
    </div>
  );
}

function SpendRow({ slice }: { slice: SpendSlice }) {
  return (
    <div className="lrow" data-testid={`overview-spend-row-${slice.key}`}>
      <span className="id">{slice.pipeline}</span>
      <span>
        <span className="ov-work" title={slice.work} data-testid={`overview-spend-work-${slice.key}`}>
          {slice.work}
        </span>
        <span className="meter" aria-hidden>
          <span style={{ width: `${Math.round(slice.share * 100)}%` }} />
        </span>
      </span>
      <span className="meta">
        ${slice.amountUsd.toFixed(2)} · {Math.round(slice.share * 100)}%
      </span>
    </div>
  );
}
