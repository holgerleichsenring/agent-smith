"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import type { SeriesRunRow } from "@/types/seriesRuns";
import { age } from "@/components/tickets/ticketFormat";
import { SeriesProgress } from "./SeriesProgress";
import { seriesNextStep, seriesStateWord } from "./seriesNextStep";

// 2026-11-23-31dcc: one series on the list — name, project and state over the next step, the progress bar with its
// counts, the held count, and how long ago it last moved. The whole row opens the series' page.

export function SeriesRunListRow({ row }: { row: SeriesRunRow }) {
  const t = useT();
  return (
    <Link href={seriesRunPath(row.id)} className="trow sr-row" data-testid={`series-row-${row.id}`} data-state={row.state}>
      <span className={`sd ${dot(row)}`} />
      <div className="rmain">
        <div className="rt">
          <span className="ttl">{row.name}</span>
          <span className="tick">{row.project} · {seriesStateWord(row.state, t)}</span>
        </div>
        <div className="act" data-testid="series-next">{seriesNextStep(row, t)}</div>
      </div>
      <div className="sr-progress">
        <SeriesProgress counts={row.counts} />
        <small>{t("series.counts", { ...row.counts })}</small>
      </div>
      <div className={`sr-heldcnt${row.counts.held > 0 ? " hot" : ""}`} data-testid="series-held">
        {row.counts.held > 0 ? t("series.held", { count: row.counts.held }) : ""}
      </div>
      <div className="when hidesm">{age(row.lastActivityAt) ?? ""}</div>
      <span className="chev">›</span>
    </Link>
  );
}

// An ended series is calm; a running one asks for a person when a question or a ready merge waits.
function dot(row: SeriesRunRow): string {
  if (row.state === "done") return "ok";
  if (row.state === "stopped") return "idle";
  return row.question || row.nextMerge ? "need" : "run";
}
