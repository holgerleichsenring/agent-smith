"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { retryAttempt } from "@/lib/ticketsApi";
import type { TicketAttempt } from "@/types/tickets";

// 2026-10-13-9a59r: Retry unchanged — only for a newest attempt that ended on infrastructure, hidden only
// from a caller known to lack runs.control. 9a59n decides; a refusal's text is shown as the server wrote it
// (a closed ticket's points at the conversation).

export const RUNS_CONTROL = "runs.control";

type Outcome = { kind: "idle" } | { kind: "sending" } | { kind: "requested" } | { kind: "said"; text: string };

export function RetryUnchanged({ attempt }: { attempt: TicketAttempt | null }) {
  const lacks = useLacksPermission(RUNS_CONTROL);
  const [outcome, setOutcome] = useState<Outcome>({ kind: "idle" });
  if (!attempt?.endedOnInfrastructure || lacks) return null;

  const retry = async () => {
    setOutcome({ kind: "sending" });
    try {
      const answer = await retryAttempt(attempt.runId);
      setOutcome(answer.kind === "requested" ? { kind: "requested" } : { kind: "said", text: answer.refusal.text });
    } catch (thrown) {
      setOutcome({ kind: "said", text: thrown instanceof Error ? thrown.message : String(thrown) });
    }
  };

  return (
    <>
      <button type="button" className="btn" onClick={() => void retry()}
        disabled={outcome.kind === "sending" || outcome.kind === "requested"} data-testid="ticket-retry">
        Retry unchanged
      </button>
      {outcome.kind === "requested" && (
        <span className="cp-note" data-testid="ticket-retry-requested">
          Retry requested — attempt {attempt.number + 1} starts through the queue.
        </span>
      )}
      {outcome.kind === "said" && <span className="cp-note bad" data-testid="ticket-retry-refused">{outcome.text}</span>}
    </>
  );
}
