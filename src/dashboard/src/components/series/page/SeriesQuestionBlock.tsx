"use client";

import { useState } from "react";
import Link from "next/link";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SpecDialogQuestionPush } from "@/types/spec-dialog";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { DialogQuestionCard } from "@/components/dialog/DialogQuestionCard";
import { RUNS_CONTROL } from "@/components/tickets/detail/RetryUnchanged";
import { holdPullRequestHref, isAsked } from "@/components/tickets/holds/holdRoutes";
import { holdSentence } from "@/components/tickets/holds/holdWords";
import type { SeriesNodeKind } from "./seriesNodeKind";

// 2026-11-26-5201c: what waits for a person, pinned directly above the chat's input so it is never one message among
// many: the series' question (the record's, 9992j's card, now drawn with or without choices and naming the ticket that
// asks) and every other ticket of the series parked on a question, each with the way to its ticket page, where it is
// answered. The rail's "question for you" and the list's row lead here.
// 2026-11-26-f511a: every ticket that needs a person gets its line, not only the ones asking — an approval, a draft and a
// failure too, the tickets the status line counts as "need you" — under a "Needs you" label, each with its one link.

/** Where the rail's and the list's question links land. */
export const SERIES_QUESTION_ID = "series-question";

/** 2026-11-26-f511a: the kinds the server counts as held (SeriesRunCounts.Held) — a person's step on the ticket. */
const HELD: ReadonlySet<SeriesNodeKind> = new Set<SeriesNodeKind>(["question", "approval", "draft", "failed"]);

/** The series' tickets that need a person, less the one the series' own question names (its card says it). */
export function heldTickets(detail: SeriesRunDetail): SeriesRunTicket[] {
  return detail.tickets.filter((ticket) => HELD.has(ticket.kind) && !(detail.question && ticket.key === detail.questionTicket));
}

/** How many items the block draws — the rail counts the same ones. */
export function openQuestionCount(detail: SeriesRunDetail): number {
  return (detail.question ? 1 : 0) + heldTickets(detail).length;
}

interface Props {
  detail: SeriesRunDetail;
  /** The conversation's thread, which the card's question is addressed on; null when it is closed. */
  threadId: string | null;
  /** Sends an answer as the conversation's next message; null where nothing can be sent (closed, a decision pending, or
   *  the dialog read not on the conversation yet). */
  onAnswer: ((answer: string) => void) | null;
  failed?: boolean;
  /** A proposal waits for a decision in Work it out, where the conversation goes on; the composer is not there. */
  deciding?: boolean;
}

export function SeriesQuestionBlock({ detail, threadId, onAnswer, failed = false, deciding = false }: Props) {
  // A sent answer hides the card until a send fails or the series asks something else; the block goes with it.
  const [sent, setSent] = useState<string | null>(null);
  const held = heldTickets(detail);
  const card = detail.question !== null && !(sent === detail.question && !failed);
  if (!card && held.length === 0) return null;
  return (
    <div id={SERIES_QUESTION_ID} className="sr-questions" data-testid="series-questions">
      {card && <SeriesQuestionCard detail={detail} threadId={threadId} deciding={deciding}
        onAnswer={onAnswer && ((answer) => { setSent(detail.question); onAnswer(answer); })} />}
      {held.length > 0 && <HeldLines tickets={held} project={detail.project} />}
    </div>
  );
}

/** 2026-11-25-9992j: the question the series waits on, its choices as buttons, the recommended one first in weight. The
 *  series takes the conversation's next message as the answer, so a click sends the choice as one; the block hides it
 *  once sent and brings it back when the send failed. Only a viewer with runs.control reaches the series with a message
 *  (anyone else's starts a design turn), so only they get buttons. 2026-11-26-5201c: drawn for every question, with its
 *  asker; one without choices says it is answered below. */
export function SeriesQuestionCard({ detail, threadId, onAnswer, deciding = false }: Omit<Props, "failed">) {
  const t = useT();
  const lacks = useLacksPermission(RUNS_CONTROL, detail.project);
  const text = detail.question;
  if (!text) return null;
  const answer = lacks ? null : onAnswer;
  const buttons = answer !== null && detail.questionChoices.length > 0;
  const question: SpecDialogQuestionPush = {
    dialogId: threadId ?? "", questionId: `series-${detail.id}`, kind: buttons ? "choice" : "text", text, at: detail.startedAt,
    expiresAt: null, choices: buttons ? detail.questionChoices.map((label) => ({ label })) : [],
  };
  const hint = lacks ? t("series.chat.questionNoControl")
    : !threadId || deciding ? t("series.chat.questionClosed")
      : buttons ? t("series.chat.questionHint") : t("series.chat.answerBelow");
  return (
    <div className="sr-question" data-testid="series-question">
      <DialogQuestionCard question={question} recommended={buttons ? detail.questionRecommended : null} hint={hint} clamp
        label={detail.questionTicket ? t("series.chat.ticketAsks", { key: detail.questionTicket }) : t("series.chat.seriesAsks")}
        onAnswer={(choice) => answer?.(choice)} />
    </div>
  );
}

function HeldLines({ tickets, project }: { tickets: SeriesRunTicket[]; project: string }) {
  const t = useT();
  return (
    <div className="sr-needs" data-testid="series-needs-you">
      <p className="sr-needs-label">{t("series.chat.needsYou")}</p>
      <div className="sr-needs-lines">
        {tickets.map((ticket) => <HeldLine key={ticket.ticketId} ticket={ticket} project={project} />)}
      </div>
    </div>
  );
}

/** One ticket that needs a person: its key, its hold's sentence (its state without a hold) and its one link — the pull
 *  request on the host, the ticket page to answer its question, or the ticket page. */
function HeldLine({ ticket, project }: { ticket: SeriesRunTicket; project: string }) {
  const t = useT();
  const pullRequest = holdPullRequestHref(ticket.hold);
  const said = ticket.hold ? holdSentence(t, ticket.hold) : ticket.state;
  return (
    <p className="sr-question-line" data-testid={`series-ticket-question-${ticket.ticketId}`}>
      <span className="sr-question-who">{ticket.key}</span> {said}{" "}
      {pullRequest ? (
        <a className="tk-link" href={pullRequest} target="_blank" rel="noreferrer">{t("series.chat.openPullRequest")}</a>
      ) : (
        <Link className="tk-link" href={ticketPath(project, ticket.ticketId)}>
          {ticket.kind === "question" || isAsked(ticket.hold) ? t("series.chat.answerOnTicket") : t("series.chat.openTicket")}
        </Link>
      )}
    </p>
  );
}
