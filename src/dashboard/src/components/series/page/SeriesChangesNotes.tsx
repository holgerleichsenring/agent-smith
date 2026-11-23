"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useT } from "@/hooks/useT";
import { fetchSeriesRunChanges } from "@/lib/seriesRunsApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunChanges, SeriesRunDetail } from "@/types/seriesRuns";
import { clock } from "../../tickets/detail/flowWords";

// 2026-11-23-31dcj: Changed in this series — every revision after the first of a member ticket's spec set, newest first,
// with its ticket, number, cause and time: information, no act. Read once when the pane opens, never with the page.

type ChangesRead = { kind: "loading" } | { kind: "failed" } | { kind: "read"; changes: SeriesRunChanges };

export function SeriesChangesNotes({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  const [read, setRead] = useState<ChangesRead>({ kind: "loading" });
  useEffect(() => {
    const controller = new AbortController();
    setRead({ kind: "loading" });
    fetchSeriesRunChanges(detail.id, controller.signal)
      .then((changes) => { if (!controller.signal.aborted) setRead({ kind: "read", changes }); })
      .catch(() => { if (!controller.signal.aborted) setRead({ kind: "failed" }); });
    return () => controller.abort();
  }, [detail.id]);
  const keyOf = (ticketId: string) => detail.tickets.find((ticket) => ticket.ticketId === ticketId)?.key ?? ticketId;
  return (
    <section className="sr-changes" data-testid="series-changes">
      <h3>{t("series.changes.title")}</h3>
      <p className="msub">{t("series.changes.sub")}</p>
      {read.kind === "loading" && <p className="msub" role="status" data-testid="series-changes-loading">{t("series.changes.loading")}</p>}
      {read.kind === "failed" && <p className="msub" role="alert" data-testid="series-changes-failed">{t("series.changes.failed")}</p>}
      {read.kind === "read" && read.changes.changes.length === 0 && read.changes.unread.length === 0 && (
        <p className="msub" data-testid="series-changes-empty">{t("series.changes.empty")}</p>
      )}
      {read.kind === "read" && (
        <div className="revs">
          {read.changes.changes.map((change) => (
            <div key={`${change.ticketId}-${change.revision}`} className="rv" data-testid={`series-change-${change.ticketId}-${change.revision}`}>
              <span className="rn">{t("series.changes.revision", { number: change.revision })}</span>
              <span className="rc">
                <Link className="mono" href={ticketPath(detail.project, change.ticketId)}>{keyOf(change.ticketId)}</Link> {change.cause}
              </span>
              <span className="rw">{clock(change.at)}</span>
            </div>
          ))}
          {read.changes.unread.map((unread) => (
            <p key={unread.ticketId} className="msub" data-testid={`series-change-unread-${unread.ticketId}`}>
              {t("series.changes.unread", { ticket: keyOf(unread.ticketId), why: unread.why })}
            </p>
          ))}
        </div>
      )}
    </section>
  );
}
