"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { SpecDialogProject, SpecDialogSessionSummary } from "@/types/spec-dialog";
import { groupByDay, outcomeLabel, timeOfDay } from "./conversationDays";

// 2026-09-15-cb3e: "/spec" and "/spec new <project>" are a project picker and a button, and
// the caller's conversations are a list — open and closed, each opened by clicking it. The
// strings still travel through the ingestion endpoint, but nobody has to type them.
// 2026-09-17-c7aed: the list is the left column, grouped by day and marked with what each
// conversation filed.
// 2026-09-17-042ef: the column is the Projects page's panel card, the day is its field label
// and the marks under a conversation are its marks — the local chip that drew them is gone.
// 2026-09-18-7a05: a row offers a delete, so the ROW stops being a button — a control nested in
// a button is invalid markup and warns. The row is a container now, with the open control and
// the delete control as siblings; neither is inside the other, and neither has to stop a click
// travelling out of it.

export function DialogConversations({
  dialogId,
  sessionHere,
  projects,
  conversations,
  picked,
  onPicked,
  onStartNew,
  onOpen,
  onDelete,
}: {
  dialogId: string | null;
  sessionHere: string | null;
  projects: SpecDialogProject[];
  conversations: SpecDialogSessionSummary[];
  picked: string;
  onPicked: (project: string) => void;
  onStartNew: (project?: string) => void;
  onOpen: (sessionId: string, openDialogId: string | null) => void;
  onDelete: (sessionId: string) => void;
}) {
  // Minting a dialog id and opening a session on it are two steps, and a second click
  // between them mints a second id: the first session opens on an id nothing is watching
  // while the page sits on the second with no session and no explanation. One click per
  // dialog id, released when the page is looking at one.
  const [starting, setStarting] = useState(false);
  useEffect(() => setStarting(false), [dialogId]);

  return (
    <section
      data-testid="dialog-controls"
      className="ecard inert @3xl:col-span-2 @6xl:col-span-1"
    >
      <div className="d-head">
        <h2 className="ec-name sans">Conversations</h2>
      </div>
      <div className="d-body flex flex-col gap-3">
        {projects.length > 1 && (
          <select
            data-testid="dialog-project-picker"
            aria-label="Project"
            value={picked}
            onChange={(event) => onPicked(event.target.value)}
            className="d-input"
          >
            <option value="">pick a project…</option>
            {projects.map((project) => (
              <option key={project.name} value={project.name}>
                {project.name}
              </option>
            ))}
          </select>
        )}
        <button
          type="button"
          data-testid="dialog-new"
          disabled={starting}
          onClick={() => {
            setStarting(true);
            onStartNew(picked || undefined);
          }}
          className="btn primary w-full"
        >
          + New conversation
        </button>
        {conversations.length > 0 && (
          <div data-testid="dialog-conversations" className="flex flex-col gap-3">
            {groupByDay(conversations).map((day) => (
              <div key={day.label} data-testid="dialog-conversation-day" className="flex flex-col gap-0.5">
                <h3 className="fl px-1 pb-1">{day.label}</h3>
                {day.conversations.map((conversation) => (
                  <Conversation
                    key={conversation.sessionId}
                    conversation={conversation}
                    current={conversation.sessionId === sessionHere}
                    onOpen={onOpen}
                    onDelete={onDelete}
                  />
                ))}
              </div>
            ))}
          </div>
        )}
        {/* 2026-09-21-f237b: the panel is not the only way to a conversation any more. */}
        <Link href="/spec-dialog/conversations" className="d-link" data-testid="dialog-see-all">
          All conversations
        </Link>
      </div>
    </section>
  );
}

function untitled({ sessionId }: SpecDialogSessionSummary): string {
  return `untitled ${sessionId}`;
}

function Conversation({
  conversation,
  current,
  onOpen,
  onDelete,
}: {
  conversation: SpecDialogSessionSummary;
  current: boolean;
  onOpen: (sessionId: string, openDialogId: string | null) => void;
  onDelete: (sessionId: string) => void;
}) {
  const filed = outcomeLabel(conversation);
  const time = timeOfDay(conversation.lastActivityAt);
  // 2026-09-21-f237a: the ROW says what the conversation is about, the DELETE says what the
  // person wrote — opposite preferences over the same two strings, on purpose. A row twenty-five
  // characters wide cannot show enough of an opening sentence to tell one conversation from
  // another; a confirmation that quoted a model-minted subject would ask someone to approve the
  // loss of something they have never seen under that name. Where only one of the two exists,
  // both fall back to it, so neither ever says "untitled" about a row that is showing a name.
  const title = conversation.subject ?? conversation.title ?? untitled(conversation);
  const written = conversation.title ?? conversation.subject ?? untitled(conversation);
  return (
    <div className="d-conv-row">
      <button
        type="button"
        data-testid={`dialog-conversation-${conversation.sessionId}`}
        aria-current={current ? "true" : undefined}
        onClick={() => onOpen(conversation.sessionId, conversation.openDialogId)}
        className="d-conv"
      >
        <span className="block truncate dsh-body font-medium text-ink">{title}</span>
        <span className="ec-marks ec-sub items-center">
          <span className="ec-mark given">{conversation.project}</span>
          <span>
            {conversation.turns} turn{conversation.turns === 1 ? "" : "s"}
          </span>
          {time && <span>{time}</span>}
          {filed && (
            <span data-testid="dialog-conversation-outcome" className="ec-mark filed">
              {filed}
            </span>
          )}
        </span>
      </button>
      <button
        type="button"
        data-testid={`dialog-delete-${conversation.sessionId}`}
        aria-label={`Delete ${written}`}
        title="Delete this conversation"
        onClick={() => onDelete(conversation.sessionId)}
        className="d-conv-x"
      >
        ×
      </button>
    </div>
  );
}
