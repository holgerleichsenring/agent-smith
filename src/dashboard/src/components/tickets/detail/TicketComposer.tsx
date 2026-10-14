"use client";

import { useRef, useState } from "react";
import { DialogComposer } from "@/components/dialog/DialogComposer";
import { DialogQuestionCard } from "@/components/dialog/DialogQuestionCard";
import { DialogWorking } from "@/components/dialog/DialogWorking";
import { approvalEffectLabel } from "@/lib/tickets/approvalEffect";
import type { Read } from "@/lib/tickets/TicketPageReads";
import type { TicketApprovalPreview, TicketAttempt, TicketConversation } from "@/types/tickets";
import { RetryUnchanged } from "./RetryUnchanged";
import { useTicketComposer, type ResumeRefusal } from "./useTicketComposer";

// 2026-10-13-9a59r: "Continue on this ticket" — the ticket's Work it out session, the same hook, composer
// and approval question as Work it out. The effect line renders the preview's effect and start arm, one
// label each; with no open approval question it is a plain composer. Approve answers the approval question
// with approve, Discard with reject, Ask for changes focuses the text field. Rendered only when 9a59h does
// not hide the conversation — the server's marker, not the client identity, gates it.

interface Props {
  conversation: TicketConversation;
  project: string;
  ticketId: string;
  preview: Read<TicketApprovalPreview>;
  newest: TicketAttempt | null;
}

const REFUSAL_WORDS: Record<ResumeRefusal["kind"], string> = {
  "question-pending": "A question is waiting in this conversation: answer the waiting question first.",
  "turn-running": "A reply is being written; try again when it lands.",
  forbidden: "",
  "not-found": "",
};

export function TicketComposer({ conversation, project, ticketId, preview, newest }: Props) {
  const composer = useTicketComposer(conversation, project, ticketId);
  const { dialog, engaged, approval, refusal } = composer;
  const input = useRef<HTMLTextAreaElement>(null);
  const [attachFirst, setAttachFirst] = useState(false);
  // Before the first act the approval question is not watched yet: an open session with a proposal is
  // one whose approval waits there, so the line shows from the preview and the click is the act.
  const waiting = engaged ? approval !== null : conversation.proposal !== null && conversation.openDialogId !== null;
  const label = waiting && preview.data ? approvalEffectLabel(preview.data) : null;
  const other = engaged && dialog.question && !approval ? dialog.question : null;

  return (
    <div className="composer" data-testid="ticket-composer">
      <div className="cp-head">
        <h3>Continue on this ticket</h3>
        <span className="sh-sub">The ticket&apos;s conversation in Work it out: one per ticket, the same thread every attempt reads.</span>
      </div>
      {(label || refusal || composer.missed) && (
        <div className="cp-body">
          {label && (
            <div className="cp-effect" data-testid="ticket-effect">
              <span><b>{label.effect}</b>{label.start ? ` ${label.start}` : ""}</span>
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
      {other && (
        <div className="cp-body mock-dialog">
          <DialogQuestionCard question={other} proposal={dialog.proposal}
            onAnswer={(answer, decision) => void dialog.send(answer, project, decision)} />
        </div>
      )}
      <div className="cp-actions">
        {label?.approvable && (
          <button type="button" className="btn primary" data-testid="ticket-approve"
            onClick={() => composer.act({ kind: "decide", decision: "approved" })}>Approve</button>
        )}
        {label && (
          <>
            <button type="button" className="btn" data-testid="ticket-ask-changes"
              onClick={() => { composer.act({ kind: "focus" }); input.current?.focus(); }}>Ask for changes</button>
            <button type="button" className="btn" data-testid="ticket-discard"
              onClick={() => composer.act({ kind: "decide", decision: "rejected" })}>Discard</button>
          </>
        )}
        <RetryUnchanged attempt={newest} />
      </div>
      {dialog.failure && !refusal && engaged && (
        <div className="cp-note bad" data-testid="ticket-composer-failure">{dialog.failure.message}</div>
      )}
      <div className="cp-input mock-dialog" onFocus={() => composer.act({ kind: "focus" })}>
        <DialogComposer
          inputRef={input}
          onSend={(text) => composer.act({ kind: "send", text })}
          onAttach={(file) => setAttachFirst(!composer.attach(file))}
          note={attachFirst
            ? { tone: "refused", text: "Send a message first: it is what binds the conversation to this ticket." }
            : dialog.uploadNote}
          disabled={refusal !== null}
        />
      </div>
    </div>
  );
}
