"use client";

import { useState } from "react";
import Link from "next/link";
import type { DeliveryAttempt, DeliveryPeriod, DeliveryTicket } from "@/lib/deliveryApi";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import { OUTCOME_LABEL } from "@/lib/delivery/deliveryFormat";
import { DeliveryTip, type TipAt } from "./DeliveryTip";

// 2026-10-25-0099c: one row per ticket, one bar per attempt in its outcome's colour, a dot on a rework attempt and
// a mark where the pull request merged. Hovering or focusing a bar says when, how long, why it ended and its cost.

const TICK_HOURS: Readonly<Record<DeliveryPeriod, number>> = { day: 4, week: 24, month: 120 };
const LEGEND = ["delivered", "shortfall", "failed", "cancelled", "running", "waiting"] as const;

export function DeliveryTimeline({ tickets, from, to, period }: { tickets: DeliveryTicket[]; from: string; to: string; period: DeliveryPeriod }) {
  const [tip, setTip] = useState<TipAt | null>(null);
  const start = Date.parse(from);
  const span = Math.max(1, Date.parse(to) - start);
  const at = (iso: string) => Math.min(100, Math.max(0, ((Date.parse(iso) - start) / span) * 100));
  return (
    <section className="panel" aria-labelledby="delivery-tl" data-testid="delivery-timeline">
      <div className="panel-head">
        <h2 id="delivery-tl">Timeline <span className="range">{from.slice(5, 16).replace("T", " ")} → {to.slice(5, 16).replace("T", " ")} UTC</span></h2>
        <div className="legend">
          {LEGEND.map((o) => <span key={o}><i className={o} />{OUTCOME_LABEL[o]}</span>)}
          <span>● Rework</span>
          <span className="merged-key">◆ PR merged</span>
        </div>
      </div>
      <div className="tl-scroll">
        <div className="tl">
          <Axis start={start} span={span} period={period} />
          {tickets.length === 0 && <p className="sub">No ticket was worked on in this period.</p>}
          {tickets.map((t) => (
            <div className="trow" key={`${t.project}/${t.ticketId}`} data-testid={`delivery-row-${t.ticketId}`}>
              <Link className="tlabel" href={ticketPath(t.project, t.ticketId)}>
                <b>{t.ticketId} {t.title}</b>
                <small>{t.project} · {t.attempts.length} attempt{t.attempts.length === 1 ? "" : "s"}</small>
              </Link>
              <div className="lane">
                {t.attempts.map((a, i) => (
                  <Bar key={a.runId} attempt={a} left={at(a.startedAt)} right={at(a.finishedAt ?? to)}
                    label={`Attempt ${i + 1}: ${OUTCOME_LABEL[a.outcome]}`}
                    show={(x, y) => setTip({ ticket: t, attempt: a, index: i, x, y })} hide={() => setTip(null)} />
                ))}
                {t.mergedAt && <span className="mergemark" style={{ left: `${at(t.mergedAt)}%` }} data-testid="delivery-merged" />}
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
  const step = TICK_HOURS[period] * 3600e3;
  const ticks = Array.from({ length: Math.floor(span / step) + 1 }, (_, i) => start + i * step);
  const label = (t: number) => {
    const iso = new Date(t).toISOString();
    return period === "day" ? `${iso.slice(11, 13)}:00` : iso.slice(5, 10);
  };
  return (
    <div className="axis">
      <div className="k">Ticket</div>
      <div className="ticks">
        {ticks.map((t) => <span key={t} style={{ left: `${((t - start) / span) * 100}%` }}>{label(t)}</span>)}
      </div>
    </div>
  );
}
