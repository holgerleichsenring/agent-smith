// 2026-11-23-31dcc: where the series list and one series' page live, apart from the reads, so a module mock of the
// series API never takes the links with it.

export const SERIES_PATH = "/series";

export function seriesRunPath(id: string): string {
  return `${SERIES_PATH}/${encodeURIComponent(id)}`;
}
