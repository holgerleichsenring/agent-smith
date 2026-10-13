import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import type { TicketCounts } from "@/types/tickets";
import { age, money } from "./ticketFormat";

// 2026-10-13-9a59p: the ticket list's six tiles, every value from the server's counts — over every
// ticket read, never only the page.

interface Tile {
  label: string;
  value: ReactNode;
  small?: string | null;
  hot?: boolean;
  testId: string;
}

function tilesOf(c: TicketCounts): Tile[] {
  const oldest = age(c.oldestNeedsYouSince);
  const done = c.doneThisMonth;
  return [
    { label: "Needs you", value: c.needsYou, small: oldest ? `oldest ${oldest}` : null, hot: c.needsYou > 0, testId: "ticket-metric-needs-you" },
    { label: "Working", value: c.working, small: `${c.running} running · ${c.queued} queued`, testId: "ticket-metric-working" },
    { label: "Waiting on review", value: c.waitingOnReview, small: `${c.openPullRequests} PRs open`, testId: "ticket-metric-review" },
    { label: "Stuck", value: c.stuck, small: "no one acted since", testId: "ticket-metric-stuck" },
    {
      label: "Done this month",
      value: done.tickets,
      small: done.tickets > 0 ? `${(done.attempts / done.tickets).toFixed(1)} attempts each` : null,
      testId: "ticket-metric-done",
    },
    { label: "Spent this month", value: money(c.monthCost), small: "all runs", testId: "ticket-metric-spent" },
  ];
}

export function TicketMetricStrip({ counts }: { counts: TicketCounts }) {
  return (
    <div className="health" data-testid="ticket-metric-strip">
      {tilesOf(counts).map((tile) => (
        <div key={tile.testId} className={cn("metric", tile.hot && "hot")} data-testid={tile.testId}>
          <span className="k">{tile.label}</span>
          <span className="v">
            {tile.value}
            {tile.small && <small>{tile.small}</small>}
          </span>
        </div>
      ))}
    </div>
  );
}
