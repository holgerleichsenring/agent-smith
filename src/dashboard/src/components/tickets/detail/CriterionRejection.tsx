"use client";

import Link from "next/link";
import { useT } from "@/hooks/useT";
import { runPath } from "@/lib/runPath";
import type { TicketSeriesCriterion } from "@/types/tickets";
import { rejectionOf } from "./specWords";

// 2026-10-16-86e6e: a criterion not met says why, under its text, with the way to the run it was reported in.
// 2026-11-18-c8e2c: the why is the master's report; a person's own judgement of the line is recorded on the run page.

interface Props {
  criterion: TicketSeriesCriterion;
  /** The run whose acceptance gave the verdict; no link without one. */
  judgedBy: string | null | undefined;
}

export function CriterionRejection({ criterion, judgedBy }: Props) {
  const t = useT();
  const reason = rejectionOf(criterion);
  if (!reason) return null;
  return (
    <span className="reject" data-testid="criterion-rejection">
      <span><b>{t("tickets.rejection.notMet")}</b> {reason}</span>
      {judgedBy && <Link className="btn sm" href={runPath(judgedBy)} data-testid="criterion-overrule">{t("tickets.rejection.openRun")}</Link>}
    </span>
  );
}
