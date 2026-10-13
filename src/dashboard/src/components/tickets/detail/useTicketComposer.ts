"use client";

import { useEffect, useRef, useState } from "react";
import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";
import { INSTANCE_DIALOG_ID } from "@/lib/specDialogSession";
import { useSpecDialog, type SpecDialogState } from "@/hooks/useSpecDialog";
import type { SpecDialogDecision, SpecDialogQuestionPush } from "@/types/spec-dialog";
import type { TicketConversation } from "@/types/tickets";

// 2026-10-13-9a59r: the ticket's Work it out session behind the composer. It runs on a dialog id of its
// own (INSTANCE_DIALOG_ID), so the browser's held Work it out id is never read or rewritten. VIEWING WRITES
// NOTHING: until the first act (focus, Send, Approve, Discard) nothing is opened, resumed or posted and the
// closed session is what 9a59h's transcript shows. On that act it goes to 9a59h's openDialogId, else
// resumes the sessionId (any dialog.write holder, 9a59m), else binds the minted id with the first message
// (project + ticket id); the act itself runs once the session is there. A refused resume is a state.

export type ComposerAct =
  | { kind: "focus" }
  | { kind: "send"; text: string }
  | { kind: "decide"; decision: SpecDialogDecision };

export type ResumeRefusal =
  | { kind: "question-pending" }
  | { kind: "turn-running" }
  | { kind: "forbidden"; reason: string }
  | { kind: "not-found"; reason: string };

export interface TicketComposerState {
  dialog: SpecDialogState;
  engaged: boolean;
  /** The approval question, while it is open and not past its deadline. */
  approval: SpecDialogQuestionPush | null;
  refusal: ResumeRefusal | null;
  /** A decision clicked before the session was there, whose question had gone by then. */
  missed: boolean;
  act: (act: ComposerAct) => void;
  retry: () => void;
  /** An image goes to the session once it is there; before the first message binds one there is none. */
  attach: (file: File) => boolean;
}

export function useTicketComposer(conversation: TicketConversation, project: string, ticketId: string): TicketComposerState {
  const dialog = useSpecDialog(INSTANCE_DIALOG_ID);
  const [engaged, setEngaged] = useState(false);
  const [queued, setQueued] = useState<ComposerAct | null>(null);
  const [missed, setMissed] = useState(false);
  const failureAtEngage = useRef<Error | null>(null);
  const engagedOnce = useRef(false);
  const { sessionId, openDialogId } = conversation;

  const engage = () => {
    if (engagedOnce.current) return;
    engagedOnce.current = true;
    failureAtEngage.current = dialog.failure;
    setEngaged(true);
    if (sessionId) void dialog.open(sessionId, openDialogId);
  };

  const ready = engaged && (sessionId
    ? dialog.view?.session?.sessionId === sessionId
    : dialog.dialogId !== null && dialog.view !== null);
  const approval = engaged && isOpenApproval(dialog.question) ? dialog.question : null;

  useEffect(() => {
    if (!ready || !queued) return;
    setQueued(null);
    if (queued.kind === "send") {
      void dialog.send(queued.text, project, undefined, sessionId ? undefined : ticketId);
    } else if (queued.kind === "decide") {
      if (isOpenApproval(dialog.question)) void dialog.send(queued.decision === "approved" ? "approve" : "reject", project, queued.decision);
      else setMissed(true);
    }
  }, [ready, queued, dialog, project, ticketId, sessionId]);

  const act = (next: ComposerAct) => {
    setMissed(false);
    engage();
    if (next.kind === "focus") return;
    setQueued(next);
  };

  const retry = () => {
    failureAtEngage.current = dialog.failure;
    if (sessionId) void dialog.open(sessionId, openDialogId);
  };

  const attach = (file: File) => {
    if (!ready || !dialog.view?.session) return false;
    void dialog.attach(file, project);
    return true;
  };

  const failure = engaged && dialog.failure !== failureAtEngage.current ? dialog.failure : null;
  return { dialog, engaged, approval, refusal: resumeRefusalOf(failure), missed, act, retry, attach };
}

export function isOpenApproval(question: SpecDialogQuestionPush | null): question is SpecDialogQuestionPush {
  return question !== null && question.kind === "approval"
    && (question.expiresAt === null || Date.parse(question.expiresAt) > Date.now());
}

const QUESTION_WAITING = "waiting for an answer";

/** The resume route's refusals (SpecDialogResumeEndpoints): 409 question waiting or turn running, 403, 404. */
export function resumeRefusalOf(failure: Error | null): ResumeRefusal | null {
  if (!failure) return null;
  const path = failure instanceof ApiResponseError || failure instanceof ApiRefusal ? failure.path : "";
  if (!path.endsWith("/resume")) return null;
  if (failure instanceof ApiRefusal) return { kind: "forbidden", reason: failure.message.replace(`${path}: `, "") };
  const error = failure as ApiResponseError;
  if (error.status === 409) {
    return error.reason?.includes(QUESTION_WAITING) ? { kind: "question-pending" } : { kind: "turn-running" };
  }
  if (error.status === 403) return { kind: "forbidden", reason: error.reason ?? "this conversation is not yours to open here" };
  return { kind: "not-found", reason: error.reason ?? "the ticket's conversation was not found" };
}
