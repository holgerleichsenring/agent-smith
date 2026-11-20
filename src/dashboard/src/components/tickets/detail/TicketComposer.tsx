"use client";

import { useEffect, useRef, useState, type MutableRefObject, type RefObject } from "react";
import { DialogComposer } from "@/components/dialog/DialogComposer";
import { DialogDecisionBar, asideKey, decides } from "@/components/dialog/DialogDecisionBar";
import { DialogQuestionCard } from "@/components/dialog/DialogQuestionCard";
import { DialogWorking } from "@/components/dialog/DialogWorking";
import { approvalProposal } from "@/components/dialog/approvalProposal";
import type { Read } from "@/lib/tickets/TicketPageReads";
import type { TicketApprovalPreview, TicketConversation } from "@/types/tickets";
import { ticketDecision } from "./ticketDecision";
import type { TicketReply } from "./ticketReply";
import { useTicketComposer, type ResumeRefusal } from "./useTicketComposer";

// 2026-11-16-6877a: no heading of its own; it sits under the Your move box and its placeholder says what a reply does.
// 2026-10-13-9a59r: "Continue on this ticket" — the ticket's Work it out session, the same hook, composer
// and approval question as Work it out; with no open approval question it is a plain composer. Rendered only
// when 9a59h does not hide the conversation — the server's marker, not the client identity, gates it.
// 2026-11-18-d6d2d: Retry unchanged left the action row; it sits in the newest attempt's report.
// 2026-11-19-1b72c: decides like Work it out (7c42b) — a decision or text, never both. An offered proposal takes the
// composer's place with its named action ("Write spec to ticket", "Post comment", "Create bug ticket") and Keep chatting,
// which gives the composer back for that proposal until the next turn has finished. No Approve, Discard or Ask for
// changes: discarding is said in words. A proposal that cannot be approved here (a list of tickets, a pending run
// question) shows its line above the composer instead.

interface Props {
  conversation: TicketConversation;
  project: string;
  ticketId: string;
  preview: Read<TicketApprovalPreview>;
  /** 2026-11-16-6877a: the text field, focused by the page's Your move box. */
  inputRef?: RefObject<HTMLTextAreaElement | null>;
  /** 2026-11-19-1b72c: set to this composer's reply — the decision set aside, then the text field focused. */
  replyRef?: MutableRefObject<TicketReply | null>;
}

const REFUSAL_WORDS: Record<ResumeRefusal["kind"], string> = {
  "question-pending": "A question is waiting in this conversation: answer the waiting question first.",
  "turn-running": "A reply is being written; try again when it lands.",
  forbidden: "",
  "not-found": "",
};

export function TicketComposer({ conversation, project, ticketId, preview, inputRef, replyRef }: Props) {
  const composer = useTicketComposer(conversation, project, ticketId);
  const { dialog, engaged, approval, refusal } = composer;
  const own = useRef<HTMLTextAreaElement>(null);
  const input = inputRef ?? own;
  const [attachFirst, setAttachFirst] = useState(false);
  // The proposal whose decision the operator set aside to keep chatting, until the agent's next turn has finished.
  const [chattingOn, setChattingOn] = useState<string | null>(null);
  useEffect(() => {
    if (!dialog.working) setChattingOn(null);
  }, [dialog.working]);
  const shown = ticketDecision(conversation, preview.data, engaged ? { question: dialog.question, awaiting: dialog.awaitingProposal } : null);
  const deciding = shown !== null && decides(shown.decision, chattingOn, shown.proposalId);
  // A decision that cannot be taken here says why above the composer; one set aside for chatting says nothing.
  const blocked = shown && !shown.decision.approvable && !shown.decision.expired ? shown.decision.summary : null;
  const other = engaged && dialog.question && !approval ? dialog.question : null;
  // 2026-11-10-ccd9 review: an approval card shows only for the awaiting proposal whose id it carries.
  const otherProposal = approvalProposal(other, dialog.awaitingProposal);
  // Keep chatting, and any reply asked for from elsewhere on the page: set the decision aside, then focus the field
  // once it is mounted again.
  const keepChatting = () => {
    if (deciding) setChattingOn(asideKey(shown?.proposalId));
    setTimeout(() => input.current?.focus());
  };
  // Published after render and withdrawn on unmount, so a page whose composer is gone falls back to its own focus.
  useEffect(() => {
    if (!replyRef) return;
    replyRef.current = keepChatting;
    return () => {
      replyRef.current = null;
    };
  });

  return (
    <div className="composer" data-testid="ticket-composer">
      {(blocked || refusal || composer.missed) && (
        <div className="cp-body">
          {blocked && (
            <div className="cp-effect" data-testid="ticket-effect">
              <span>{blocked}</span>
            </div>
          )}
          {refusal && (
            <div className="cp-effect person" data-testid={`ticket-resume-${refusal.kind}`}>
              <span>{REFUSAL_WORDS[refusal.kind] || ("reason" in refusal ? refusal.reason : "")}</span>
              <button type="button" className="btn sm" onClick={composer.retry}>Try again</button>
            </div>
          )}
          {composer.missed && (
            <div className="cp-effect person" data-testid="ticket-approval-gone">
              <span>The approval question is no longer waiting — write below to ask for the proposal again.</span>
            </div>
          )}
        </div>
      )}
      {dialog.working && (
        <div className="cp-body mock-dialog" data-testid="ticket-working">
          <DialogWorking readings={dialog.readings} activity={dialog.activity} since={dialog.workingSince} />
        </div>
      )}
      {other && (other.kind !== "approval" || otherProposal) && (
        <div className="cp-body mock-dialog">
          <DialogQuestionCard question={other} onAnswer={(answer) => void dialog.send(answer, project, undefined, undefined, other.questionId)} />
        </div>
      )}
      {dialog.failure && !refusal && engaged && (
        <div className="cp-note bad" data-testid="ticket-composer-failure">{dialog.failure.message}</div>
      )}
      {deciding && shown ? (
        <div className="chat-box mock-dialog" data-testid="ticket-decision">
          <DialogDecisionBar decision={shown.decision} onKeepChatting={keepChatting}
            onApprove={() => composer.act({ kind: "approve", proposalId: shown.proposalId ?? undefined })} />
        </div>
      ) : (
        <div className="chat-box mock-dialog" data-testid="ticket-chat-box" onFocus={() => composer.act({ kind: "focus" })}>
          <DialogComposer
            inputRef={input}
            onSend={(text) => composer.act({ kind: "send", text })}
            onAttach={(file) => setAttachFirst(!composer.attach(file))}
            note={attachFirst
              ? { tone: "refused", text: "Send a message first: it is what binds the conversation to this ticket." }
              : dialog.uploadNote}
            disabled={refusal !== null}
            placeholder="Reply to ask for a change. A reply starts the next run."
          />
        </div>
      )}
    </div>
  );
}
