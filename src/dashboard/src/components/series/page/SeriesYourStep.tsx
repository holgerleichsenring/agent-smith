"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { seriesRunPath } from "@/lib/seriesRunAddress";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { seriesMergeLine, seriesStepLines } from "./seriesStepLines";

// 2026-11-23-849e: the block at the top of every series section — what the person does next, from the holds
// (seriesStepLines), and whether merging is what moves the rest. Nothing to do says so.

export function SeriesYourStep({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const lines = seriesStepLines(detail, seriesRunPath(detail.id), t);
  const merges = seriesMergeLine(detail, t);
  return (
    <section className="sr-you" data-testid="series-your-step" aria-label={t("series.you.title")}>
      <div className="sr-you-title">{t("series.you.title")}</div>
      {lines.length === 0 ? <div className="sr-you-line" data-testid="series-your-step-nothing">{t("series.you.nothing")}</div>
        : lines.map((line) => (
          <div className="sr-you-line" key={line.ticketId} data-testid={`series-your-step-${line.ticketId}`}>
            <span>{line.text}</span>
            {line.act.external
              ? <a className="btn sm primary" href={line.act.href} target="_blank" rel="noreferrer">{line.act.label}</a>
              : <Link className="btn sm primary" href={line.act.href}>{line.act.label}</Link>}
          </div>
        ))}
      {merges && <div className="sr-you-merges" data-testid="series-your-step-merges">{merges}</div>}
    </section>
  );
}
