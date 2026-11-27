"use client";

import { useState } from "react";
import Link from "next/link";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useT } from "@/hooks/useT";
import { ticketPath } from "@/lib/tickets/ticketAddress";
import type { SeriesRunDetail, SeriesRunTicket } from "@/types/seriesRuns";
import { RUNS_CONTROL } from "@/components/tickets/detail/RetryUnchanged";
import { holdPullRequestHref, isAsked } from "@/components/tickets/holds/holdRoutes";
import { holdSentence } from "@/components/tickets/holds/holdWords";
import type { SeriesNodeKind } from "./seriesNodeKind";
import { SeriesQuestionLine } from "./SeriesQuestionLine";
import { SeriesQuickReplies } from "./SeriesQuickReplies";

// 2026-11-26-5201c: what waits for a person, directly above the chat's input. 2026-11-26-f511a: every ticket that needs a
// person gets its line under a "Needs you" label, each with its one link — an approval, a draft and a failure too, the
// tickets the status line counts as "need you".
// 2026-11-27-3532e: no pinned question card any more — the master's question is its plain message in the chat, its choices
// are quick replies here, and this block carries the anchor the status bar links to. A ticket whose run's question its
// series master holds (3532b: a nobody hold) is the master's, not a person's line. Where no replies are drawn the question
// stands as one line (SeriesQuestionLine) — in full with its choices where no chat is drawn — so the anchor never lands on
// an empty block.

/** Where the rail's and the list's question links land. */
export const SERIES_QUESTION_ID = "series-question";

/** 2026-11-26-f511a: the kinds the server counts as held (SeriesRunCounts.Held) — a person's step on the ticket. */
const HELD: ReadonlySet<SeriesNodeKind> = new Set<SeriesNodeKind>(["question", "approval", "draft", "failed"]);

/** The series' tickets that need a person — less a ticket whose run's question its master holds. */
export function heldTickets(detail: SeriesRunDetail): SeriesRunTicket[] {
  return detail.tickets.filter((ticket) => HELD.has(ticket.kind) && !(isAsked(ticket.hold) && ticket.hold?.addressee === "nobody"));
}

/** How many items the block draws — the rail counts the same ones. */
export function openQuestionCount(detail: SeriesRunDetail): number {
  return (detail.question ? 1 : 0) + heldTickets(detail).length;
}

interface Props {
  detail: SeriesRunDetail;
  /** Sends a quick reply as the conversation's next message; null where nothing can be sent (closed, a decision pending,
   *  or the dialog read not on the conversation yet). */
  onAnswer: ((answer: string) => void) | null;
  failed?: boolean;
  /** No chat is drawn under it (a gone or closed conversation), so the question's own text stands here. */
  withText?: boolean;
}

export function SeriesQuestionBlock({ detail, onAnswer, failed = false, withText = false }: Props) {
  // A sent reply hides the replies until a send fails or the series asks something else; the pointer stands meanwhile.
  const [sent, setSent] = useState<string | null>(null);
  const lacks = useLacksPermission(RUNS_CONTROL, detail.project);
  const held = heldTickets(detail);
  // The same rule as openQuestionCount: the block stands while the series' question or a person's ticket waits.
  if (detail.question === null && held.length === 0) return null;
  const replying = detail.question !== null && !withText && !lacks && onAnswer !== null && detail.questionChoices.length > 0
    && !(sent === detail.question && !failed);
  return (
    <div id={SERIES_QUESTION_ID} className="sr-questions" data-testid="series-questions">
      {detail.question !== null && !replying && <SeriesQuestionLine detail={detail} full={withText} />}
      {replying && onAnswer && <SeriesQuickReplies detail={detail} onAnswer={(answer) => { setSent(detail.question); onAnswer(answer); }} />}
      {held.length > 0 && <HeldLines tickets={held} project={detail.project} />}
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
