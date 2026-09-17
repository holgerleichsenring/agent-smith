"use client";

import { useEffect, useState } from "react";
import type { SpecDialogProject, SpecDialogSessionSummary } from "@/types/spec-dialog";
import { groupByDay, outcomeLabel, timeOfDay } from "./conversationDays";

// 2026-09-15-cb3e: "/spec" and "/spec new <project>" are a project picker and a button, and
// the caller's conversations are a list — open and closed, each opened by clicking it. The
// strings still travel through the ingestion endpoint, but nobody has to type them.
// 2026-09-17-c7aed: the list is the left column, grouped by day and marked with what each
// conversation filed.

export function DialogConversations({
  dialogId,
  sessionHere,
  projects,
  conversations,
  picked,
  onPicked,
  onStartNew,
  onOpen,
}: {
  dialogId: string | null;
  sessionHere: string | null;
  projects: SpecDialogProject[];
  conversations: SpecDialogSessionSummary[];
  picked: string;
  onPicked: (project: string) => void;
  onStartNew: (project?: string) => void;
  onOpen: (sessionId: string, openDialogId: string | null) => void;
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
      className="rounded-md border border-mute bg-canvas @3xl:col-span-2 @6xl:col-span-1"
    >
      <div className="border-b border-mute px-3 py-2">
        <h2 className="dsh-body font-semibold text-ink">Conversations</h2>
      </div>
      <div className="flex flex-col gap-3 p-2.5">
        {projects.length > 1 && (
          <select
            data-testid="dialog-project-picker"
            aria-label="Project"
            value={picked}
            onChange={(event) => onPicked(event.target.value)}
            className="rounded-md border border-mute bg-canvas px-2 py-1 dsh-body text-ink"
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
          className="w-full rounded-md bg-primary-deep px-3 py-2 text-left dsh-body font-semibold text-on-primary hover:bg-primary-pressed disabled:opacity-50"
        >
          + New conversation
        </button>
        {conversations.length > 0 && (
          <div data-testid="dialog-conversations" className="flex flex-col gap-3">
            {groupByDay(conversations).map((day) => (
              <div key={day.label} data-testid="dialog-conversation-day" className="flex flex-col gap-0.5">
                <h3 className="eyebrow-uppercase px-1 pb-1 text-body">{day.label}</h3>
                {day.conversations.map((conversation) => (
                  <Conversation
                    key={conversation.sessionId}
                    conversation={conversation}
                    current={conversation.sessionId === sessionHere}
                    onOpen={onOpen}
                  />
                ))}
              </div>
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

function Conversation({
  conversation,
  current,
  onOpen,
}: {
  conversation: SpecDialogSessionSummary;
  current: boolean;
  onOpen: (sessionId: string, openDialogId: string | null) => void;
}) {
  const filed = outcomeLabel(conversation);
  const time = timeOfDay(conversation.lastActivityAt);
  return (
    <button
      type="button"
      data-testid={`dialog-conversation-${conversation.sessionId}`}
      aria-current={current ? "true" : undefined}
      onClick={() => onOpen(conversation.sessionId, conversation.openDialogId)}
      className="w-full rounded-md border-l-2 border-transparent px-2 py-1.5 text-left hover:bg-canvas-soft aria-[current=true]:border-primary-deep aria-[current=true]:bg-canvas-soft"
    >
      <span className="block truncate dsh-body font-medium text-ink">
        {conversation.title ?? `untitled ${conversation.sessionId}`}
      </span>
      <span className="mt-0.5 flex flex-wrap items-center gap-1.5 dsh-label text-body">
        <Chip>{conversation.project}</Chip>
        <span>
          {conversation.turns} turn{conversation.turns === 1 ? "" : "s"}
        </span>
        {time && <span>{time}</span>}
        {filed && (
          <Chip testId="dialog-conversation-outcome" filed>
            {filed}
          </Chip>
        )}
      </span>
    </button>
  );
}

function Chip({
  children,
  filed,
  testId,
}: {
  children: string;
  filed?: boolean;
  testId?: string;
}) {
  return (
    <span
      data-testid={testId}
      className={
        filed
          ? "rounded-sm border border-primary-deep px-1 font-mono text-primary-deep"
          : "rounded-sm border border-mute px-1 font-mono text-body"
      }
    >
      {children}
    </span>
  );
}
