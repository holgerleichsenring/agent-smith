import type { DeliveryEndCause } from "@/lib/deliveryApi";
import { causeLabel, causeOutcome } from "@/lib/delivery/deliveryFormat";

// 2026-10-25-0099c: why the period's attempts ended, most frequent first, each bar in its outcome's colour.

export function DeliveryEndCauses({ causes }: { causes: DeliveryEndCause[] }) {
  const max = Math.max(1, ...causes.map((c) => c.count));
  return (
    <section className="panel" aria-labelledby="delivery-why" data-testid="delivery-why">
      <div className="panel-head"><h2 id="delivery-why">Why attempts ended</h2></div>
      {causes.length === 0 ? (
        <p className="sub">No attempt ended in this period.</p>
      ) : (
        <div className="hbars">
          {causes.map((c) => (
            <div className="hbar" key={c.cause} data-testid={`delivery-cause-${c.cause}`}>
              <span className="why">{causeLabel(c.cause)}</span>
              <span className="track">
                <span className={`fill ${causeOutcome(c.cause)}`} style={{ width: `${(c.count / max) * 100}%` }} />
              </span>
              <span className="n">{c.count}</span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
