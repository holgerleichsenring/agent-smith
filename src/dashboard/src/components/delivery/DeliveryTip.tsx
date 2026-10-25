import type { DeliveryAttempt, DeliveryTicket } from "@/lib/deliveryApi";
import { OUTCOME_LABEL, causeLabel, formatAt, formatDuration, formatUsd } from "@/lib/delivery/deliveryFormat";

// 2026-10-25-0099c: what one attempt was — when, how long, why it ended, its specs and its cost.

export interface TipAt {
  ticket: DeliveryTicket;
  attempt: DeliveryAttempt;
  index: number;
  x: number;
  y: number;
}

export function DeliveryTip({ tip }: { tip: TipAt }) {
  const { ticket, attempt, index } = tip;
  return (
    <div className="tip" role="tooltip" data-testid="delivery-tip" style={{ left: tip.x + 12, top: Math.max(8, tip.y - 92) }}>
      <b>{ticket.ticketId} · attempt {index + 1}{attempt.rework ? " · rework" : ""}</b>
      <br />
      {formatAt(attempt.startedAt)} → {attempt.finishedAt ? formatAt(attempt.finishedAt) : "running"}{" "}
      <span className="m">({formatDuration(attempt.seconds)})</span>
      <br />
      {attempt.cause ? causeLabel(attempt.cause) : OUTCOME_LABEL[attempt.outcome]}
      <br />
      <span className="m">specs {attempt.specsDone}/{attempt.specsTotal} · {formatUsd(attempt.costUsd)}</span>
    </div>
  );
}
