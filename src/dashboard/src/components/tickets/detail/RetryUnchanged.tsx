"use client";

import { useState } from "react";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { retryAttempt } from "@/lib/ticketsApi";
import type { TicketAttempt } from "@/types/tickets";

// 2026-10-13-9a59r: Retry unchanged, hidden only from a caller known to lack runs.control. 9a59n decides; a
// refusal's text is shown as the server wrote it (a closed ticket's points at the conversation).
// 2026-11-17-166ad: offered for a finished newest attempt that is neither a success nor handed back — the
// server retries any such end, not only an infrastructure one.
// 2026-11-18-d6d2d: onAnswer tells the page how the request settled — the run page follows a requested Retry to the
// new attempt and re-reads the ticket after a refusal.

export const RUNS_CONTROL = "runs.control";

type Outcome = { kind: "idle" } | { kind: "sending" } | { kind: "requested" } | { kind: "said"; text: string };

export type RetrySettled = "requested" | "refused";

interface Props {
  attempt: TicketAttempt | null;
  project: string;
  onAnswer?: (settled: RetrySettled) => void;
}

export function RetryUnchanged({ attempt, project, onAnswer }: Props) {
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [outcome, setOutcome] = useState<Outcome>({ kind: "idle" });
  if (!attempt || !retriable(attempt) || lacks) return null;

  const retry = async () => {
    setOutcome({ kind: "sending" });
    try {
      const answer = await retryAttempt(attempt.runId);
      setOutcome(answer.kind === "requested" ? { kind: "requested" } : { kind: "said", text: answer.refusal.text });
      onAnswer?.(answer.kind === "requested" ? "requested" : "refused");
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

/** 2026-11-17-166ad: the ends Retry starts again — finished, not a success, not handed back. */
export function retriable(attempt: TicketAttempt): boolean {
  return attempt.finishedAt !== null && attempt.status !== "success" && !attempt.handedBack;
}
