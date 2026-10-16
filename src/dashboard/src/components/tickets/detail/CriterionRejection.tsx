"use client";

import Link from "next/link";
import { runPath } from "@/lib/runPath";
import type { TicketSeriesCriterion } from "@/types/tickets";
import { rejectionOf } from "./specWords";

// 2026-10-16-86e6e: a criterion the acceptance gate did not accept says why, under its text, with the way to the run
// whose judgement the page reads back — where the operator can overrule it (a judgement there is a label; it does not
// re-run the gate).

interface Props {
  criterion: TicketSeriesCriterion;
  /** The run whose acceptance gave the verdict; no link without one. */
  judgedBy: string | null | undefined;
}

export function CriterionRejection({ criterion, judgedBy }: Props) {
  const reason = rejectionOf(criterion);
  if (!reason) return null;
  return (
    <span className="reject" data-testid="criterion-rejection">
      <span><b>Not accepted:</b> {reason}</span>
      {judgedBy && <Link className="btn sm" href={runPath(judgedBy)} data-testid="criterion-overrule">Overrule in the run</Link>}
    </span>
  );
}
