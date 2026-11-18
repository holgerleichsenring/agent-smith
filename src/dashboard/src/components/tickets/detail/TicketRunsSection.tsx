"use client";

import Link from "next/link";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketAttempt } from "@/types/tickets";
import { causeLabel, causeWhere } from "./attemptStory";
import { clock } from "./flowWords";
import { runListLines } from "./runLines";
import { addressQuery, CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-11-17-8d1cc: the Runs section — one line per attempt and derivation (runListLines), each opening its run, and
// under an attempt the change that started it, whose "read" goes to What happened at that cause (?causeOf=). The next
// run starts from What happened; nothing here starts one.

export function TicketRunsSection() {
  const detail = useTicketDetail();
  const lines = runListLines(detail.data?.attempts ?? [], detail.data?.derivations ?? [], detail.data?.otherRuns ?? []);
  if (lines.length === 0) return <div className="msub">No run is recorded.</div>;
  return (
    <div className="tk-runs" data-testid="runs-list">
      {lines.map((line) => (
        <div key={line.runId} className={`tk-run ${line.tone}`} data-testid={line.attempt ? `run-line-${line.attempt.number}` : `run-line-${line.runId}`}>
          <Link className="tk-run-line" href={line.href} data-testid={`run-link-${line.runId}`}>
            <span className="tk-dot" aria-hidden="true" />
            <span className="tk-who">{line.who}</span>
            <span className="tk-text"><span className="tk-outcome">{line.outcome}</span> · {line.text}</span>
            <span className="tk-meta mono">{line.meta}</span>
            <span className="tk-chev" aria-hidden="true">›</span>
          </Link>
          {line.attempt && <ChangeEvent attempt={line.attempt} project={detail.project} ticketId={detail.ticketId} />}
        </div>
      ))}
    </div>
  );
}

/** The change that started `attempt`, under its line. */
function ChangeEvent({ attempt, project, ticketId }: { attempt: TicketAttempt; project: string; ticketId: string }) {
  const address = useTicketTabGo();
  const label = causeLabel(attempt);
  if (!label) return null;
  const who = [attempt.cause?.actor, causeWhere(attempt)].filter(Boolean).join(" on ");
  const anchored = !!attempt.cause?.source?.id;
  const extra = { [CAUSE_PARAM]: anchored ? attempt.runId : null };
  const query = addressQuery("", "happened", extra);
  return (
    <div className="tk-run-cause" data-testid={`attempt-change-${attempt.number}`}>
      Started by {label}
      {who && <> · {who}</>}
      {attempt.cause?.at && <> · {clock(attempt.cause.at)}</>}
      {" · "}
      <a href={query ? `${ticketPath(project, ticketId)}?${query}` : ticketPath(project, ticketId)} data-testid={`attempt-change-read-${attempt.number}`}
        onClick={(e) => {
          e.preventDefault();
          address.go("happened", extra);
        }}>read</a>
    </div>
  );
}
