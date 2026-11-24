"use client";

import type { ReactNode } from "react";
import { useLanguage, useT } from "@/hooks/useT";
import type { PocDetail, PocLifetime } from "@/lib/pocsApi";
import { pocDate } from "../pocDate";
import { ExtendButton } from "./LifetimePanel";

// 2026-11-23-c81ee: the dashboard notice is the reminder channel. While the window is open it says when the PoC
// expires and how many days are left; once it expired, why it is stopped and on which date it is deleted unless it is
// extended. Extend shows when the server says the caller may; Delete when the PoC's state admits it. The detail page
// posts the seen reminder; this banner only shows it.

export function LifetimeBanner({ poc, lifetime, busy, onExtend, onDelete, children }: {
  poc: PocDetail; lifetime: PocLifetime; busy: boolean; onExtend: () => void; onDelete: () => void; children?: ReactNode;
}) {
  const t = useT();
  const language = useLanguage();
  if (!lifetime.windowOpen && !lifetime.expired) return null;
  const date = pocDate(lifetime.expiresAt, language);
  return (
    <section className={`poc-alert poc-life-banner${lifetime.expired ? " expired" : ""}`} role="status"
      data-testid="poc-detail-banner" data-lifetime={lifetime.expired ? "expired" : "expiring"}>
      {lifetime.expired ? (
        <>
          <h2>{t("pocs.detail.banner.expired", { name: poc.name, date })}</h2>
          <p data-testid="poc-detail-banner-why">
            {lifetime.deletesAt
              ? t("pocs.detail.banner.expiredUnseen", { date: pocDate(lifetime.deletesAt, language) })
              : t("pocs.detail.banner.expiredDue")}
            {!poc.acts.start && <>{" "}{t("pocs.detail.banner.startAfterExtend")}</>}
          </p>
        </>
      ) : (
        <>
          <h2>{t("pocs.detail.banner.expiring", { name: poc.name, date, count: lifetime.daysLeft })}</h2>
          <p>{t("pocs.detail.banner.expiringHelp")}</p>
        </>
      )}
      {(lifetime.mayExtend || (lifetime.expired && poc.acts.delete)) && (
        <div className="poc-bar">
          {lifetime.mayExtend && <ExtendButton busy={busy} onExtend={onExtend} testId="poc-detail-banner-extend" />}
          {lifetime.expired && poc.acts.delete && (
            <button type="button" className="btn danger" disabled={busy} data-testid="poc-detail-banner-delete" onClick={onDelete}>
              {t("pocs.detail.act.delete")}
            </button>
          )}
        </div>
      )}
      {children}
    </section>
  );
}
