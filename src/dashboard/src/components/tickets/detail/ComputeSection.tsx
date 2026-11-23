"use client";

import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";
import { duration, type ComputeTotals, type PersonWait } from "./computeTotals";

// 2026-10-13-9a59s: the Compute section — four figures and the waiting line. 2026-11-17-8d1cc: plain key/value lines;
// the per-attempt wall-clock bars are gone (each run's pods are on its run page).

export function ComputeTiles({ totals }: { totals: ComputeTotals }) {
  const t = useT();
  const locale = useDateLocale();
  const runEnd = Math.round(totals.podMinutesToRunEnd);
  const clippedNote = totals.unobserved > 0 && runEnd !== Math.round(totals.podMinutes)
    ? ` · ${t("tickets.compute.clipped", { count: totals.unobserved, runEnd })}`
    : "";
  const waits = totals.waits.length;
  return (
    <div className="tk-kv" data-testid="compute-tiles">
      <div className="tk-kv-row"><span className="k">{t("tickets.compute.podTime")}</span>
        <span className="v">{duration(totals.podMinutes * 60000, t)} <span className="tk-dim" data-testid="compute-pod-note">{t("tickets.compute.podMinutes", { n: Math.round(totals.podMinutes) })}{clippedNote}</span></span></div>
      <div className="tk-kv-row"><span className="k">{t("tickets.compute.memory")}</span>
        <span className="v" data-testid="compute-reserved">
          {totals.reservedGiHours !== null
            ? `${totals.reservedGiHours.toLocaleString(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 })} Gi·h` : "—"}{" "}
          <span className="tk-dim">{totals.reservedGiHours !== null ? t("tickets.compute.reservation") : t("tickets.compute.noReservation")}</span>
        </span></div>
      <div className="tk-kv-row"><span className="k">{t("tickets.compute.modelTime")}</span>
        <span className="v" data-testid="compute-model">{duration(totals.modelMs, t)} <span className="tk-dim">{t("tickets.compute.modelNote")}</span></span></div>
      <div className={totals.waitingMs > 0 ? "tk-kv-row hot" : "tk-kv-row"} data-testid="compute-waiting"><span className="k">{t("tickets.compute.waitingOnYou")}</span>
        <span className="v">{duration(totals.waitingMs, t)} <span className="tk-dim">{t("tickets.compute.waits", { count: waits })}
          {totals.capacityMs > 0 ? ` · ${t("tickets.compute.capacity", { span: duration(totals.capacityMs, t) })}` : ""}</span></span></div>
    </div>
  );
}

function waitWords(t: T, wait: PersonWait): string {
  const took = duration(wait.to - wait.from, t);
  const span = wait.open ? t("tickets.cost.soFar", { amount: took }) : took;
  return wait.kind === "handback"
    ? t("tickets.compute.waitHandback", { n: wait.attempt, span })
    : t("tickets.compute.waitQuestion", { n: wait.attempt, span });
}

/** 2026-11-22-4321a: onRead gets the run of the first wait, so the conversation opens unfolded at that attempt. */
export function WaitingBanner({ totals, onRead }: { totals: ComputeTotals; onRead: (runId: string) => void }) {
  const t = useT();
  if (totals.waits.length === 0 || totals.waitingMs <= totals.modelMs) return null;
  const n = totals.waits.length;
  return (
    <div className="tk-wait-line" data-testid="compute-banner">
      <span className="tk-dot" aria-hidden="true" />
      <span>
        {t("tickets.compute.bannerHead", { count: n })}{" "}
        {totals.waits.map((wait) => waitWords(t, wait)).join("; ")}.{" "}
        {totals.podsHeldWhileWaiting ? t("tickets.compute.podsHeld") : t("tickets.compute.noPodsHeld")}{" "}
        <button className="tk-link" type="button" onClick={() => onRead(totals.waits[0].runId)} data-testid="compute-banner-read">{n === 2 ? t("tickets.compute.readBoth") : t("tickets.compute.readInConversation")}</button>
      </span>
    </div>
  );
}
