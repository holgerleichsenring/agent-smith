"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import type { SpecDialogActivityPush } from "@/types/spec-dialog";
import { condenseSteps } from "./condenseSteps";
import { waitsFor } from "./graphTicketWaits";
import { courseOf, markOf } from "./turnCourse";
import { rowsOf, type TurnProposal } from "./turnProposal";
import { TurnQuestions } from "./TurnQuestions";
import { useTurnSteps } from "./useTurnSteps";

// 2026-11-12-92e9c: one agent turn as one message that never changes once written — its steps as quiet lines in the
// order they came, the reply, one line per spec with the review's mark, what a bug, comment or list would file, the
// open questions as text, and one course line. Nothing in it is a control: a past proposal's detail lives in its run,
// which the course line links to, and the decision on the awaiting one is taken outside the turn.

export function TurnMessage({
  reply,
  proposal,
  runId,
  live,
}: {
  /** The reply as rendered by the transcript, or the note of a turn that did not finish. */
  reply?: ReactNode;
  proposal?: TurnProposal | null;
  runId?: string | null;
  /** The steps this page watched the turn take; a past turn's are read from its run instead. */
  live?: SpecDialogActivityPush[];
}) {
  const { steps, firstAt, lastAt } = useTurnSteps(runId, live);
  const lines = condenseSteps(steps);
  const course = courseOf(proposal ?? null, firstAt, lastAt);
  return (
    <div className="flex min-w-0 flex-col gap-2" data-testid="turn-message" data-kind={proposal?.kind}>
      {lines.length > 0 && (
        <ul className="flex flex-col gap-0.5 font-mono dsh-mono text-mute" data-testid="turn-steps">
          {lines.map((line) => (
            <li key={line.key} data-testid="turn-step" data-kind={line.kind}>{line.text}</li>
          ))}
        </ul>
      )}
      {reply}
      {proposal && <Outcome proposal={proposal} />}
      {(course.length > 0 || runId) && (
        <p className="font-mono dsh-mono text-mute" data-testid="turn-course">
          {course.join(" · ")}
          {runId && (
            <>
              {course.length > 0 ? " · " : ""}
              <Link className="d-link" href={`/jobs/${encodeURIComponent(runId)}`} data-testid="turn-course-run">run</Link>
            </>
          )}
        </p>
      )}
    </div>
  );
}

function Outcome({ proposal }: { proposal: TurnProposal }) {
  const rows = rowsOf(proposal);
  return (
    <>
      {rows.length > 0 && (
        <ul className="flex flex-col gap-0.5 dsh-body" data-testid="turn-spec-lines">
          {rows.map((row) => {
            const mark = markOf(proposal, row.specId);
            return (
              <li key={row.specId} data-testid={`turn-spec-${row.specId}`} data-mark={mark.word} className="text-body">
                <span aria-hidden="true" className="inline-block w-4 text-ink">{mark.mark}</span>
                <span className="sr-only">{mark.word}: </span>
                <span className="font-mono dsh-label">{row.specId}</span> {row.goal}
                {mark.reason && <span className="text-ink"> — {mark.reason}</span>}
              </li>
            );
          })}
        </ul>
      )}
      {proposal.bug && <p className="dsh-body text-ink" data-testid="turn-bug">Bug: {proposal.bug.title}</p>}
      {proposal.kind === "comment" && proposal.comment && (
        <p className="dsh-body whitespace-pre-wrap text-body" data-testid="turn-comment">{proposal.comment}</p>
      )}
      {(proposal.tickets?.length ?? 0) > 0 && (
        <ul className="flex flex-col gap-0.5 dsh-body text-body" data-testid="turn-ticket-lines">
          {proposal.tickets!.map((ticket) => (
            <li key={ticket.key}>
              <span className="font-mono dsh-label text-ink">{ticket.key}</span> {ticket.title} · {waitsFor(ticket)}
            </li>
          ))}
        </ul>
      )}
      <TurnQuestions questions={proposal.questions ?? []} />
    </>
  );
}
