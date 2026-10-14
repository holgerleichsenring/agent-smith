"use client";

import { useEffect, useState } from "react";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import { AttemptFlowGraph } from "./AttemptFlowGraph";
import { CarriedBanner, SummaryCells } from "./SummaryCells";

// 2026-10-13-9a59q: the Summary tab — four cells and the carried/new banner for the chosen attempt,
// then that attempt's flow graph. The newest attempt is chosen until the reader picks another.

export function TicketSummaryTab() {
  const detail = useTicketDetail().data;
  const attempts = detail?.attempts ?? [];
  const newest = attempts.length > 0 ? attempts[attempts.length - 1].runId : null;
  const [picked, setPicked] = useState<string | null>(null);
  const [followNewest, setFollowNewest] = useState(true);
  useEffect(() => {
    if (followNewest) setPicked(newest);
  }, [newest, followNewest]);
  if (!detail) return null;
  const attempt = attempts.find((a) => a.runId === (picked ?? newest)) ?? attempts[attempts.length - 1];
  if (!attempt) return <div className="msub">No attempt is recorded.</div>;
  const choose = (runId: string) => {
    setFollowNewest(runId === newest);
    setPicked(runId);
  };
  return (
    <div data-testid="ticket-summary">
      <SummaryCells detail={detail} attempt={attempt} />
      <CarriedBanner attempt={attempt} />
      <AttemptFlowGraph detail={detail} attempt={attempt} onSelect={choose} />
    </div>
  );
}
