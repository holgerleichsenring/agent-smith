import type { SpecDialogSpend } from "@/types/spec-dialog";
import { usd } from "./turnProposal";

// 2026-11-10-d854b: a proposal's work in one line — its review rounds and what it cost. 2026-11-12-92e9c: it no longer
// pages a run's steps; a turn's steps are its message's own lines (TurnMessage), and the ticket page shows none.

export function TurnActivityLine({ rounds, spend }: { rounds: number; spend?: SpecDialogSpend | null }) {
  // Nothing to say: nothing spent and no review — a proposal stored before either was kept.
  if (!spend && rounds === 0) return null;
  return (
    <div className="d-turn-activity" data-testid="turn-activity-line">
      <span>{rounds > 0 ? `${rounds} review ${rounds === 1 ? "round" : "rounds"}` : "Not reviewed"}</span>
      {spend && <span className="cost" data-testid="turn-activity-cost">{usd(spend.usd)}</span>}
    </div>
  );
}
