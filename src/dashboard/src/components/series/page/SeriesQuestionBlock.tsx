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
import { isAsked } from "@/components/tickets/holds/holdRoutes";
import { holdSentence } from "@/components/tickets/holds/holdWords";

// 2026-11-26-5201c: what waits for a person, pinned directly above the chat's input so it is never one message among
// many: the series' question (the record's, 9992j's card, now drawn with or without choices and naming the ticket that
// asks) and every other ticket of the series parked on a question, each with the way to its ticket page, where it is
// answered. The rail's "question for you" and the list's row lead here.

/** Where the rail's and the list's question links land. */
export const SERIES_QUESTION_ID = "series-question";

/** The series' tickets parked on a question that is not the series' own question. */
export function ticketQuestions(detail: SeriesRunDetail): SeriesRunTicket[] {
  return detail.tickets.filter((ticket) => isAsked(ticket.hold) && !(detail.question && ticket.key === detail.questionTicket));
}

/** How many questions the block draws — the rail counts the same ones. */
export function openQuestionCount(detail: SeriesRunDetail): number {
  return (detail.question ? 1 : 0) + ticketQuestions(detail).length;
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
  const asked = ticketQuestions(detail);
  const card = detail.question !== null && !(sent === detail.question && !failed);
  if (!card && asked.length === 0) return null;
  return (
    <div id={SERIES_QUESTION_ID} className="sr-questions" data-testid="series-questions">
      {card && <SeriesQuestionCard detail={detail} threadId={threadId} deciding={deciding}
        onAnswer={onAnswer && ((answer) => { setSent(detail.question); onAnswer(answer); })} />}
      {asked.map((ticket) => <TicketQuestionLine key={ticket.ticketId} ticket={ticket} project={detail.project} />)}
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
      <p className="sr-question-who" data-testid="series-question-who">
        {detail.questionTicket ? t("series.chat.ticketAsks", { key: detail.questionTicket }) : t("series.chat.seriesAsks")}
      </p>
      <DialogQuestionCard question={question} recommended={buttons ? detail.questionRecommended : null} hint={hint}
        onAnswer={(choice) => answer?.(choice)} />
    </div>
  );
}

function TicketQuestionLine({ ticket, project }: { ticket: SeriesRunTicket; project: string }) {
  const t = useT();
  return (
    <p className="sr-question-line" data-testid={`series-ticket-question-${ticket.ticketId}`}>
      <span className="sr-question-who">{ticket.key}</span> {ticket.hold ? holdSentence(t, ticket.hold) : ""}{" "}
      <Link className="tk-link" href={ticketPath(project, ticket.ticketId)}>{t("series.chat.answerOnTicket")}</Link>
    </p>
  );
}
