"use client";

import { useEffect, useRef, useState } from "react";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useTicketApprovalPreview, useTicketConversation, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt } from "@/types/tickets";
import { causeEntryOf } from "./causeEntryMatch";
import { ConversationList } from "./ConversationList";
import { ProposalBlock } from "./ProposalBlock";
import { RetryUnchanged, RUNS_CONTROL } from "./RetryUnchanged";
import { TicketComposer } from "./TicketComposer";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-10-13-9a59r: the Conversation tab — every exchange of the ticket in order (ticket comments and edits,
// questions and answers, PR reviews, the spec cut and the ticket's Work it out conversation), the open
// proposal, and the composer. Reads come from the page's store only; the approval preview is the one read
// this tab adds. A pending run question heads the tab.
// 2026-10-17-e730a: the composer sits in a sticky dock at the bottom of the scroll box, so the thread scrolls under it.
// 2026-10-17-e730b: once per mount, when the conversation is there, the tab opens at its end — unless a run question
// waits (its card is shown) or ?causeOf= names an entry (the list scrolls to that). The end anchor after the dock is
// what is scrolled: the stuck dock is already drawn at the bottom, so scrolling to it would move nothing.

export function TicketConversationTab() {
  const detail = useTicketDetail();
  const conversation = useTicketConversation();
  const preview = useTicketApprovalPreview();
  const address = useTicketTabGo();
  const attempts = detail.data?.attempts ?? [];
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const asking = [...attempts].reverse().find((a) => a.pendingQuestion) ?? null;
  const data = conversation.data;
  const causeOf = address.param(CAUSE_PARAM);
  const ticketUrl = detail.data?.summary.webUrl ?? null;

  const question = useRef<HTMLDivElement>(null);
  const end = useRef<HTMLDivElement>(null);
  const opened = useRef(false);
  useEffect(() => {
    if (!data || !detail.data || opened.current) return;
    opened.current = true;
    const cause = causeOf ? data.attempts.find((a) => a.runId === causeOf)?.cause : undefined;
    if (causeOf && causeEntryOf(data.entries, cause, ticketUrl)) return;
    if (asking && question.current) question.current.scrollIntoView({ block: "start" });
    else end.current?.scrollIntoView({ block: "end" });
  }, [data, detail.data, causeOf, ticketUrl, asking]);

  if (!detail.data) return null;

  return (
    <>
      <div className="section-head first">
        <h2>Conversation</h2>
        {data && <span className="cnt">{data.entries.length}</span>}
        <span className="sh-sub">ticket comments, questions and answers, PR reviews, the spec cut review and the ticket conversation, in order</span>
      </div>
      {asking && <div ref={question}><PendingQuestion attempt={asking} project={detail.project} /></div>}
      {!data && (
        <div className="msub" data-testid="conv-unread">
          {conversation.loading ? "Reading the conversation…" : "The conversation could not be read."}
        </div>
      )}
      {data && (
        <>
          {data.threadUnavailable && (
            <div className="msub note" data-testid="conv-thread-unavailable">The ticket&apos;s own comments could not be read from its tracker.</div>
          )}
          <ConversationList conversation={data} detail={detail.data} causeOf={causeOf} />
          {data.proposal && !data.transcriptHidden && <ProposalBlock proposal={data.proposal} preview={preview.data} />}
          <div className="conv-dock" data-testid="conv-dock">
            <div className="conv-dock-body">
              {data.transcriptHidden ? (
                <div className="composer hidden-note" data-testid="conv-hidden">
                  <div className="cp-head">
                    <h3>Continue on this ticket</h3>
                    <span className="sh-sub">The ticket conversation needs dialog.write: its turns and proposal are not shown.</span>
                  </div>
                  <div className="cp-actions"><RetryUnchanged attempt={newest} project={detail.project} /></div>
                </div>
              ) : (
                <TicketComposer conversation={data} project={detail.project} ticketId={detail.ticketId}
                  preview={preview} newest={newest} />
              )}
            </div>
          </div>
          <div ref={end} data-testid="conv-end" />
        </>
      )}
    </>
  );
}

function PendingQuestion({ attempt, project }: { attempt: TicketAttempt; project: string }) {
  const lacks = useLacksPermission(RUNS_CONTROL, project);
  const [answered, setAnswered] = useState(false);
  const question = attempt.pendingQuestion;
  if (!question) return null;
  if (lacks) {
    return (
      <div className="q-item q-head" data-testid="conv-question-text">
        <div className="qt">{question.text}</div>
        <div className="qm">Attempt {attempt.number} waits for this answer; answering needs runs.control.</div>
      </div>
    );
  }
  return (
    <div className="q-head" data-testid="conv-question">
      <PendingQuestionCard runId={attempt.runId} question={question} answered={answered}
        onAnswered={() => setAnswered(true)} label={`Send & resume attempt ${attempt.number}`} />
    </div>
  );
}
