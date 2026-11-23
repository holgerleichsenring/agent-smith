"use client";

import { useState } from "react";
import Link from "next/link";
import type { DeliveryAttempt, DeliveryPeriod, DeliveryTicket } from "@/lib/deliveryApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import { useT } from "@/hooks/useT";
import { outcomeLabel } from "@/lib/delivery/deliveryFormat";
import { DeliveryTip, type TipAt } from "./DeliveryTip";

// 2026-10-25-0099c: one row per ticket, one bar per attempt in its outcome's colour, a dot on a rework attempt and
// a mark where the pull request merged. Hovering or focusing a bar says when, how long, why it ended and its cost.

const TICK_HOURS: Readonly<Record<DeliveryPeriod, number>> = { day: 4, week: 24, month: 120 };
const LEGEND = ["delivered", "shortfall", "failed", "cancelled", "running", "waiting"] as const;

export function DeliveryTimeline({ tickets, from, to, period }: { tickets: DeliveryTicket[]; from: string; to: string; period: DeliveryPeriod }) {
  const t = useT();
  const [tip, setTip] = useState<TipAt | null>(null);
  const start = Date.parse(from);
  const span = Math.max(1, Date.parse(to) - start);
  const at = (iso: string) => Math.min(100, Math.max(0, ((Date.parse(iso) - start) / span) * 100));
  return (
    <section className="panel" aria-labelledby="delivery-tl" data-testid="delivery-timeline">
      <div className="panel-head">
        <h2 id="delivery-tl">{t("delivery.timeline.title")} <span className="range">{from.slice(5, 16).replace("T", " ")} → {to.slice(5, 16).replace("T", " ")} UTC</span></h2>
        <div className="legend">
          {LEGEND.map((o) => <span key={o}><i className={o} />{outcomeLabel(t, o)}</span>)}
          <span>● {t("delivery.timeline.rework")}</span>
          <span className="merged-key">◆ {t("delivery.timeline.merged")}</span>
        </div>
      </div>
      <div className="tl-scroll">
        <div className="tl">
          <Axis start={start} span={span} period={period} />
          {tickets.length === 0 && <p className="sub">{t("delivery.timeline.empty")}</p>}
          {tickets.map((ticket) => (
            <div className="trow" key={`${ticket.project}/${ticket.ticketId}`} data-testid={`delivery-row-${ticket.ticketId}`}>
              <Link className="tlabel" href={ticketPath(ticket.project, ticket.ticketId)}>
                <b>{ticket.ticketId} {ticket.title}</b>
                <small>{ticket.project} · {t("delivery.timeline.attempts", { count: ticket.attempts.length })}</small>
              </Link>
              <div className="lane">
                {ticket.attempts.map((a, i) => (
                  <Bar key={a.runId} attempt={a} left={at(a.startedAt)} right={at(a.finishedAt ?? to)}
                    label={t("delivery.timeline.bar", { index: i + 1, outcome: outcomeLabel(t, a.outcome) })}
                    show={(x, y) => setTip({ ticket, attempt: a, index: i, x, y })} hide={() => setTip(null)} />
                ))}
                {ticket.mergedAt && <span className="mergemark" style={{ left: `${at(ticket.mergedAt)}%` }} data-testid="delivery-merged" />}
              </div>
            </div>
          ))}
        </div>
      </div>
      {tip && <DeliveryTip tip={tip} />}
    </section>
  );
}

function Bar({ attempt, left, right, label, show, hide }: {
  attempt: DeliveryAttempt; left: number; right: number; label: string; show: (x: number, y: number) => void; hide: () => void;
}) {
  return (
    <button type="button" className={`bar ${attempt.outcome}${attempt.rework ? " rework" : ""}`} aria-label={label}
      style={{ left: `${left}%`, width: `${Math.max(0.6, right - left)}%` }} data-testid={`delivery-bar-${attempt.runId}`}
      onMouseMove={(e) => show(e.clientX, e.clientY)} onMouseLeave={hide}
      onFocus={(e) => { const r = e.currentTarget.getBoundingClientRect(); show(r.left, r.top); }} onBlur={hide} />
  );
}

function Axis({ start, span, period }: { start: number; span: number; period: DeliveryPeriod }) {
  const t = useT();
  const step = TICK_HOURS[period] * 3600e3;
  const ticks = Array.from({ length: Math.floor(span / step) + 1 }, (_, i) => start + i * step);
  const label = (tick: number) => {
    const iso = new Date(tick).toISOString();
    return period === "day" ? `${iso.slice(11, 13)}:00` : iso.slice(5, 10);
  };
  return (
    <div className="axis">
      <div className="k">{t("delivery.col.ticket")}</div>
      <div className="ticks">
        {ticks.map((tick) => <span key={tick} style={{ left: `${((tick - start) / span) * 100}%` }}>{label(tick)}</span>)}
      </div>
    </div>
  );
}
