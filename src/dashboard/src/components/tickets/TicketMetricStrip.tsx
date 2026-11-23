import type { ReactNode } from "react";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { cn } from "@/lib/utils";
import type { TicketCounts } from "@/types/tickets";
import { age, money } from "./ticketFormat";

// 2026-10-13-9a59p: the ticket list's six tiles, every value from the server's counts — over every
// ticket read, never only the page. 2026-11-23-d60d: no In review tile — the open pull requests are counted on their own.

interface Tile {
  label: string;
  value: ReactNode;
  small?: string | null;
  hot?: boolean;
  testId: string;
}

function tilesOf(t: T, locale: string, c: TicketCounts): Tile[] {
  const oldest = age(c.oldestNeedsYouSince, t);
  const done = c.doneThisMonth;
  return [
    { label: t("tickets.metric.needsYou"), value: c.needsYou, small: oldest ? t("tickets.metric.oldest", { age: oldest }) : null, hot: c.needsYou > 0, testId: "ticket-metric-needs-you" },
    { label: t("tickets.metric.working"), value: c.working, small: t("tickets.metric.workingSmall", { running: c.running, queued: c.queued }), testId: "ticket-metric-working" },
    { label: t("tickets.metric.prs"), value: c.openPullRequests, small: t("tickets.metric.prsSmall"), testId: "ticket-metric-prs" },
    { label: t("tickets.metric.stuck"), value: c.stuck, small: t("tickets.metric.stuckSmall"), testId: "ticket-metric-stuck" },
    {
      label: t("tickets.metric.done"),
      value: done.tickets,
      small: done.tickets > 0
        ? t("tickets.metric.doneSmall", { n: (done.attempts / done.tickets).toLocaleString(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }) })
        : null,
      testId: "ticket-metric-done",
    },
    { label: t("tickets.metric.spent"), value: money(c.monthCost), small: t("tickets.metric.spentSmall"), testId: "ticket-metric-spent" },
  ];
}

export function TicketMetricStrip({ counts }: { counts: TicketCounts }) {
  const t = useT();
  const locale = useDateLocale();
  return (
    <div className="health" data-testid="ticket-metric-strip">
      {tilesOf(t, locale, counts).map((tile) => (
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
