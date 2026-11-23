import type { DeliveryAttempt, DeliveryTicket } from "@/lib/deliveryApi";
import { useLanguage } from "@/hooks/useT";
import { causeLabel, formatAt, formatDuration, formatUsd, outcomeLabel, wording } from "@/lib/delivery/deliveryFormat";

// 2026-10-25-0099c: what one attempt was — when, how long, why it ended, its specs and its cost.

export interface TipAt {
  ticket: DeliveryTicket;
  attempt: DeliveryAttempt;
  index: number;
  x: number;
  y: number;
}

export function DeliveryTip({ tip }: { tip: TipAt }) {
  const w = wording(useLanguage());
  const { t } = w;
  const { ticket, attempt, index } = tip;
  return (
    <div className="tip" role="tooltip" data-testid="delivery-tip" style={{ left: tip.x + 12, top: Math.max(8, tip.y - 92) }}>
      <b>{ticket.ticketId} · {t("delivery.tip.attempt", { index: index + 1 })}{attempt.rework ? ` · ${t("delivery.tip.rework")}` : ""}</b>
      <br />
      {formatAt(attempt.startedAt)} → {attempt.finishedAt ? formatAt(attempt.finishedAt) : t("delivery.tip.running")}{" "}
      <span className="m">({formatDuration(w, attempt.seconds)})</span>
      <br />
      {attempt.cause ? causeLabel(t, attempt.cause) : outcomeLabel(t, attempt.outcome)}
      <br />
      <span className="m">{t("delivery.tip.specs", { done: attempt.specsDone, total: attempt.specsTotal })} · {formatUsd(w, attempt.costUsd)}</span>
    </div>
  );
}
