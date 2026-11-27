"use client";

import { useT } from "@/hooks/useT";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { SeriesFinalStep } from "./SeriesFinalStep";
import { SeriesGraph } from "./SeriesGraph";

// 2026-11-23-f562: the series page's order graph. 2026-11-25-9992j: a picture of where the series stands, nothing to
// operate — a click on a ticket opens its details to read, and the series' last step (its final pull requests) sits
// below. Every act is the master's, asked or told in the chat under it.

// 2026-11-26-b186: folded, the overview is one line — the series' counts and the way back to the graph; the graph's hint is
// its panel's title, so no line of text stands under it.
// 2026-11-27-ecf2a: the page holds what is lit (a rail row's course) and which ticket's details are open, so a node and a
// row open the same details.

/** 2026-11-26-f511b: the graph's region, which the divider under it sizes. */
export const SERIES_OVERVIEW_ID = "series-overview";

interface Props {
  detail: SeriesRunDetail;
  collapsed?: boolean;
  onToggle?: () => void;
  lit?: Set<string> | null;
  hovered?: string | null;
  openId?: string | null;
  onNode?: (ticketId: string, node: HTMLElement) => void;
  onClose?: () => void;
}

const none = () => {};

export function SeriesOverviewSection({ detail, collapsed = false, onToggle, lit = null, hovered = null, openId = null,
  onNode = none, onClose = none }: Props) {
  const t = useT();
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
    <div id={SERIES_OVERVIEW_ID} className="sr-overview">
      <SeriesGraph detail={detail} lit={lit} hovered={hovered} openId={openId} onClose={onClose} onHide={onToggle} onNode={onNode} />
      {detail.integrationBranch && <div className="sr-list" data-testid="series-final"><SeriesFinalStep detail={detail} /></div>}
    </div>
  );
}
