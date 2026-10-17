"use client";

import { useState } from "react";
import { PendingQuestionCard } from "@/components/jobs/PendingQuestionCard";
import { useLacksPermission } from "@/hooks/useLacksPermission";
import { useTicketApprovalPreview, useTicketConversation, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketAttempt } from "@/types/tickets";
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

export function TicketConversationTab() {
  const detail = useTicketDetail();
  const conversation = useTicketConversation();
  const preview = useTicketApprovalPreview();
  const address = useTicketTabGo();
  if (!detail.data) return null;
  const attempts = detail.data.attempts;
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const asking = [...attempts].reverse().find((a) => a.pendingQuestion) ?? null;
  const data = conversation.data;

  return (
    <>
      <div className="section-head first">
        <h2>Conversation</h2>
        {data && <span className="cnt">{data.entries.length}</span>}
        <span className="sh-sub">ticket comments, questions and answers, PR reviews, the spec cut review and the ticket conversation, in order</span>
      </div>
      {asking && <PendingQuestion attempt={asking} project={detail.project} />}
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
          <ConversationList conversation={data} detail={detail.data} causeOf={address.param(CAUSE_PARAM)} />
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
