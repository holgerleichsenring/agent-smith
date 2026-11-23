import { SeriesRunsHome } from "@/components/series/SeriesRunsHome";

// 2026-11-23-31dcc: the series list, in the ticket list's frame.
export default function SeriesPage() {
  return (
    <div className="mock-shell mock-tickets" data-testid="series-home">
      <main className="main">
        <SeriesRunsHome />
      </main>
    </div>
  );
}
