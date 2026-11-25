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

export function SeriesOverviewSection({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const [from, setFrom] = useState<string | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const close = useCallback(() => setOpenId(null), []);
  const lit = useMemo(() => (from ? seriesCourse(detail.edges, from) : null), [detail.edges, from]);
  return (
    <div className="sr-overview">
      <SeriesGraph detail={detail} lit={lit} from={from} onFrom={setFrom} openId={openId} onClose={close}
        onNode={(id) => setOpenId((open) => (open === id ? null : id))}
        menu={(ticket, at) => <SeriesTicketDetails ticket={ticket} project={detail.project}
          integrationBranch={detail.integrationBranch} at={at} onClose={close} />} />
      <p className="msub">{t("series.graph.hint")}</p>
      {detail.integrationBranch && <div className="sr-list" data-testid="series-final"><SeriesFinalStep detail={detail} /></div>}
    </div>
  );
}
