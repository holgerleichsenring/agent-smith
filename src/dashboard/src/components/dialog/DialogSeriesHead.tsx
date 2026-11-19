"use client";

import { useState } from "react";
import type { SeriesAct, SeriesRunView } from "@/types/series";

// 2026-11-19-1b72a: the series in the conversation head — its state, and Pause while it runs or Resume while it is
// paused. The button takes the act the series master takes when the operator says the same in words; an ended series
// has neither. A refusal is said beside it in the server's words.

export function DialogSeriesHead({ series, onAct }: { series: SeriesRunView; onAct: (act: SeriesAct) => Promise<void> }) {
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  const act: SeriesAct | null = series.state === "paused" ? "resume"
    : series.state === "running" || series.state === "verifying" ? "pause" : null;
  async function press(what: SeriesAct) {
    setBusy(true);
    setRefusal(null);
    try {
      await onAct(what);
    } catch (thrown) {
      setRefusal(thrown instanceof Error ? thrown.message : String(thrown));
    } finally {
      setBusy(false);
    }
  }
  return (
    <span className="dl-series-head">
      <span data-testid="dialog-series-state" data-state={series.state}>{series.headState}</span>
      {act && (
        <button
          type="button"
          className="dl-series-act"
          data-testid="dialog-series-act"
          disabled={busy}
          onClick={() => void press(act)}
        >
          {act === "pause" ? "Pause" : "Resume"}
        </button>
      )}
      {refusal && <span className="dl-series-refusal" role="alert">{refusal}</span>}
    </span>
  );
}
