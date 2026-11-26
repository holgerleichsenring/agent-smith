"use client";

import { useCallback, useMemo, useState } from "react";
import { useT } from "@/hooks/useT";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { SeriesFinalStep } from "./SeriesFinalStep";
import { SeriesGraph } from "./SeriesGraph";
import { SeriesTicketDetails } from "./SeriesTicketDetails";
import { seriesCourse } from "./seriesOrderLayout";

// 2026-11-23-f562: the series page's order graph. 2026-11-25-9992j: a picture of where the series stands, nothing to
// operate — a click on a ticket opens its details to read, and the series' last step (its final pull requests) sits
// below. Every act is the master's, asked or told in the chat under it.

// 2026-11-26-b186: folded, the overview is one line — the series' counts and the way back to the graph; the graph's hint is
// its panel's title, so no line of text stands under it.

export function SeriesOverviewSection({ detail, collapsed = false, onToggle }:
  { detail: SeriesRunDetail; collapsed?: boolean; onToggle?: () => void }) {
  const t = useT();
  const [from, setFrom] = useState<string | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const close = useCallback(() => setOpenId(null), []);
  const lit = useMemo(() => (from ? seriesCourse(detail.edges, from) : null), [detail.edges, from]);
  if (collapsed) {
    return (
      <div className="sr-graph-line" data-testid="series-graph-collapsed">
        <span>{t("series.counts", { ...detail.counts })}</span>
        <button type="button" className="d-link" aria-expanded={false} onClick={onToggle} data-testid="series-graph-toggle">
          {t("series.graph.show")}
        </button>
      </div>
    );
  }
  return (
    <div className="sr-overview">
      <SeriesGraph detail={detail} lit={lit} from={from} onFrom={setFrom} openId={openId} onClose={close} onHide={onToggle}
        onNode={(id) => setOpenId((open) => (open === id ? null : id))}
        menu={(ticket, at) => <SeriesTicketDetails ticket={ticket} project={detail.project} seriesId={detail.id}
          integrationBranch={detail.integrationBranch} at={at} onClose={close} />} />
      {detail.integrationBranch && <div className="sr-list" data-testid="series-final"><SeriesFinalStep detail={detail} /></div>}
    </div>
  );
}
