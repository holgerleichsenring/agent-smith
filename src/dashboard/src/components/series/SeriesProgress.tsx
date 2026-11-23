import type { SeriesRunCounts } from "@/types/seriesRuns";

// 2026-11-23-31dcc: a series' progress as one bar — merged, then pull requests waiting, then held — each a share of all
// its tickets; what is left is at work or not started.

function share(part: number, total: number): string {
  return `${total === 0 ? 0 : (100 * part) / total}%`;
}

export function SeriesProgress({ counts }: { counts: SeriesRunCounts }) {
  return (
    <div className="sr-bar" aria-hidden="true" data-testid="series-progress">
      <span className="sr-landed" style={{ width: share(counts.landed, counts.total) }} />
      <span className="sr-waiting" style={{ width: share(counts.waiting, counts.total) }} />
      <span className="sr-held" style={{ width: share(counts.held, counts.total) }} />
    </div>
  );
}
