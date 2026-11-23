"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useT } from "@/hooks/useT";
import { fetchAdminHolds } from "@/lib/holdsApi";
import { refusalIn } from "@/lib/apiResponse";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { holdSentence, sinceClock } from "@/components/tickets/holds/holdWords";
import type { AdminHoldItem, AdminHolds } from "@/types/tickets";
import { PageHead } from "./PageHead";

// 2026-11-23-31dci: every admin hold in one place, across the projects the admin sees — a precondition stop and a wait on
// nobody that outlasted holds.escalate_after_minutes alike, the longest held first. Each row names the ticket, since
// when, the hold's sentence (the one every surface shows) and, for an escalation, the value and the key it outlasted.

/** Where the escalation's minutes are set. */
export const HOLDS_SETTINGS_PATH = "/config/settings/holds";

export function NeedsAttentionView() {
  const t = useT();
  const [data, setData] = useState<AdminHolds | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    fetchAdminHolds(controller.signal)
      .then(setData)
      .catch((e: Error) => {
        if (e.name !== "AbortError") setError(e);
      });
    return () => controller.abort();
  }, []);

  const refusal = refusalIn(error);
  return (
    <div data-testid="needs-attention-view">
      <PageHead title={t("attention.title")} sub={t("attention.sub")} />
      {refusal ? (
        <RefusalSurface refusal={refusal} surface={t("attention.surface")} />
      ) : error ? (
        <div className="stateline err" data-testid="needs-attention-error">{t("attention.failed", { message: error.message })}</div>
      ) : !data ? (
        <div className="stateline" data-testid="needs-attention-loading">{t("attention.loading")}</div>
      ) : data.holds.length === 0 ? (
        <div className="empty" data-testid="needs-attention-empty">{t("attention.empty")}</div>
      ) : (
        <>
          <div className="section-head">
            <span>{t("attention.holds")}</span>
            <span className="cnt">{data.holds.length}</span>
          </div>
          <div className="list">
            {data.holds.map((item) => <HoldRow key={`${item.project}/${item.ticketId}`} item={item} />)}
          </div>
        </>
      )}
    </div>
  );
}

function HoldRow({ item }: { item: AdminHoldItem }) {
  const t = useT();
  const { hold } = item;
  const id = `${item.project}-${item.ticketId}`;
  const settings = hold.escalation ? HOLDS_SETTINGS_PATH : hold.act?.kind === "settings" ? hold.act.target : null;
  return (
    <div className="ecard" data-testid={`needs-attention-row-${id}`}>
      <div className="ec-top">
        <div>
          <Link className="ec-name sans" href={ticketPath(item.project, item.ticketId)} data-testid={`needs-attention-ticket-${id}`}>
            {item.ticketKey ?? item.ticketId}{item.title ? ` · ${item.title}` : ""}
          </Link>
          <div className="ec-sub">
            {item.project}
            {item.since && <> · {t("attention.since", { at: sinceClock(item.since) })}</>}
          </div>
        </div>
      </div>
      <div className="hint-line" data-testid={`needs-attention-sentence-${id}`}>{holdSentence(hold, t)}</div>
      {hold.escalation && (
        <div className="ec-sub" data-testid={`needs-attention-limit-${id}`}>
          {t("attention.limit", { minutes: hold.escalation.afterMinutes, key: hold.escalation.key })}
        </div>
      )}
      {settings && (
        <Link href={settings} className="btn" data-testid={`needs-attention-settings-${id}`}>{t("hold.act.settings")}</Link>
      )}
    </div>
  );
}
