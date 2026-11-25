"use client";

import { useCallback, useMemo, useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import { RUNS_CONTROL } from "../../tickets/detail/RetryUnchanged";
import type { SeriesActHandlers } from "./SeriesActButtons";
import { SeriesActionList } from "./SeriesActionList";
import { SeriesGraph } from "./SeriesGraph";
import { SeriesMergeOutcome } from "./SeriesMergeOutcome";
import { SeriesNodeMenu } from "./SeriesNodeMenu";
import { seriesOverviewCounts, type SeriesNodeKind } from "./seriesNodeKind";
import { seriesCourse, seriesNodeId } from "./seriesOrderLayout";
import { seriesNodeSentence, seriesTicketActs } from "./seriesTicketActs";
import { useSeriesMerge, type SeriesMerge } from "./useSeriesMerge";
import { useSeriesRetry } from "./useSeriesRetry";

// 2026-11-23-f562: the series page's Overview, its first section — what one act did (once, here), the summary chips, the
// order graph with a menu per ticket, and the action list below in the same colours. It replaced Merge next, Who moves
// next, Order and branches and Your next step; every count is read from the server's ticket.kind, so chips, graph and list agree.

interface Props {
  detail: SeriesRunDetail;
  reload: () => void;
  /** The page's one merge act; the pane's own when drawn alone. */
  merge?: SeriesMerge;
}

export function SeriesOverviewSection({ detail, reload, merge: shared }: Props) {
  const lacks = useLacksPermission(RUNS_CONTROL, detail.project);
  const own = useSeriesMerge(detail.id, reload);
  const merge = shared ?? own;
  const retry = useSeriesRetry(reload);
  const t = useT();
  const [from, setFrom] = useState<string | null>(null);
  const [hover, setHover] = useState<string | null>(null);
  const [openId, setOpenId] = useState<string | null>(null);
  const close = useCallback(() => setOpenId(null), []);
  const focus = hover ?? from;
  const lit = useMemo(() => (focus ? seriesCourse(detail.edges, focus) : null), [detail.edges, focus]);
  // An act taken in the menu closes it and gives focus back to its node, as Escape does.
  const done = (id: string) => { if (openId === id) { setOpenId(null); document.getElementById(seriesNodeId(id))?.focus(); } };
  const handlers: SeriesActHandlers = {
    mergeBusy: merge.busy, retryBusy: retry.busy,
    onMerge: (id) => { done(id); void merge.merge(id); },
    onRetry: (id, runId) => { done(id); void retry.retry(id, runId); },
    onRefresh: (id, url) => { done(id); void retry.refresh(id, detail.project, url); }, // 2026-11-24-9dc0e
    onChain: (id) => { done(id); setFrom(id); },
  };
  const keyOf = (id: string) => detail.tickets.find((ticket) => ticket.ticketId === id)?.key ?? id;
  return (
    <div className="sr-overview">
      {merge.outcome && (
        <div className="sr-said" data-testid="series-act-outcome">
          {"result" in merge.outcome && merge.outcome.result.kind !== "said" && <b>{keyOf(merge.outcome.ticketId)}</b>}
          <SeriesMergeOutcome outcome={merge.outcome} />
        </div>
      )}
      {retry.outcome && (
        <div className={`sr-said${"said" in retry.outcome ? " bad" : ""}`} role="status" data-testid="series-retry-outcome">
          {"said" in retry.outcome ? `${keyOf(retry.outcome.ticketId)}: ${retry.outcome.said}`
            : "refreshed" in retry.outcome ? `${keyOf(retry.outcome.ticketId)}: ${t("hold.act.refreshStarted")}`
            : t("series.act.retryRequested", { key: keyOf(retry.outcome.ticketId) })}
        </div>
      )}
      <SeriesChips detail={detail} canMerge={!lacks} />
      <SeriesGraph detail={detail} lit={lit} from={from} onFrom={setFrom} openId={openId} onClose={close}
        onNode={(id) => setOpenId((open) => (open === id ? null : id))}
        menu={(ticket, at) => {
          const kind = ticket.kind;
          return <SeriesNodeMenu ticket={ticket} kind={kind} sentence={seriesNodeSentence(ticket, detail, kind, t)} at={at}
            acts={seriesTicketActs(ticket, detail, kind, lacks, t)} handlers={handlers} onClose={close} />;
        }} />
      <p className="msub">{t("series.graph.hint")}</p>
      <SeriesActionList detail={detail} lacks={lacks} handlers={handlers} onChain={setHover} />
    </div>
  );
}

const CHIPS: SeriesNodeKind[] = ["now", "needed", "question", "failed", "running"];

function SeriesChips({ detail, canMerge }: { detail: SeriesRunDetail; canMerge: boolean }) {
  const t = useT();
  const counts = seriesOverviewCounts(detail, canMerge);
  // 2026-11-23-8071: a viewer who may not merge sees no merge-now chip; the merge is not their move.
  const shown = CHIPS.filter((kind) => counts.kinds[kind] > 0 && (canMerge || kind !== "now"));
  if (shown.length === 0) return null; // You're up says there is nothing to do
  return (
    <div className="sr-chips" data-testid="series-chips">
      {shown.map((kind) => (
        <span key={kind} className="sr-chip" data-testid={`series-chip-${kind}`}>
          <i className={`sr-dot ${kind}`} />
          {kind === "needed"
            ? t("series.chip.needed", { merges: t("series.chip.merges", { count: counts.kinds.needed }),
              tickets: t("series.chip.tickets", { count: counts.neededFor.length }) })
            : t(`series.chip.${kind as "now" | "question" | "failed" | "running"}`, { count: counts.kinds[kind] })}
        </span>
      ))}
    </div>
  );
}
