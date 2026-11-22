"use client";

import Link from "next/link";
import { useState } from "react";
import { useTicketDetail } from "@/lib/tickets/TicketPageStore";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { TicketAttempt } from "@/types/tickets";
import { DeployFollowBlock } from "../deploying/DeployFollowBlock";
import { causeLabel, causeWhere } from "./attemptStory";
import { clock } from "./flowWords";
import { runListLines, type RunListLine } from "./runLines";
import { AttemptBody } from "./ThreadMessages";
import { addressQuery, CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-11-17-8d1cc: the Runs section — one line per attempt and derivation (runListLines), each opening its run, and
// under an attempt the change that started it, whose "read" goes to the conversation at that cause (?causeOf=). The
// next run starts from Now; nothing here starts one.
// 2026-11-22-4321a: an attempt's line opens its report body with the run's sectioned log under it ("Run log"), so an
// older run's log is read here now that the conversation draws reports without it.

export function TicketRunsSection() {
  const detail = useTicketDetail();
  const lines = runListLines(detail.data?.attempts ?? [], detail.data?.derivations ?? [], detail.data?.otherRuns ?? []);
  if (lines.length === 0) return <div className="msub">No run is recorded.</div>;
  return (
    <div className="tk-runs" data-testid="runs-list">
      {lines.map((line) => <RunLine key={line.runId} line={line} project={detail.project} ticketId={detail.ticketId} />)}
    </div>
  );
}

function RunLine({ line, project, ticketId }: { line: RunListLine; project: string; ticketId: string }) {
  const [open, setOpen] = useState(false);
  const attempt = line.attempt;
  return (
    <div className={`tk-run ${line.tone}`} data-testid={attempt ? `run-line-${attempt.number}` : `run-line-${line.runId}`} data-open={open || undefined}>
      <Link className="tk-run-line" href={line.href} data-testid={`run-link-${line.runId}`}>
        <span className="tk-dot" aria-hidden="true" />
        <span className="tk-who">{line.who}</span>
        <span className="tk-text"><span className="tk-outcome">{line.outcome}</span> · {line.text}</span>
        <span className="tk-meta mono">{line.meta}</span>
        <span className="tk-chev" aria-hidden="true">›</span>
      </Link>
      {attempt && <ChangeEvent attempt={attempt} project={project} ticketId={ticketId} />}
      {attempt && (
        <button type="button" className="tk-link tk-run-open" aria-expanded={open} onClick={() => setOpen(!open)}
          data-testid={`run-log-toggle-${attempt.number}`}>
          <span aria-hidden="true">{open ? "▾" : "▸"}</span> Run log
        </button>
      )}
      {attempt && open && (
        <div className="tk-line-body" data-testid={`run-body-${attempt.number}`}>
          <AttemptBody attempt={attempt} log />
        </div>
      )}
      {line.who === "Deploy" && line.outcome !== "running" && <DeployFollowBlock runId={line.runId} />}
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
  const query = addressQuery("", "conversation", extra);
  return (
    <div className="tk-run-cause" data-testid={`attempt-change-${attempt.number}`}>
      Started by {label}
      {who && <> · {who}</>}
      {attempt.cause?.at && <> · {clock(attempt.cause.at)}</>}
      {" · "}
      <a href={query ? `${ticketPath(project, ticketId)}?${query}` : ticketPath(project, ticketId)} data-testid={`attempt-change-read-${attempt.number}`}
        onClick={(e) => {
          e.preventDefault();
          address.go("conversation", extra);
        }}>read</a>
    </div>
  );
}
