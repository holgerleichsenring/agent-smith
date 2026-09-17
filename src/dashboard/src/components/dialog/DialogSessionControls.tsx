"use client";

import { useEffect, useState } from "react";
import type { SpecDialogSessionSummary, SpecDialogView } from "@/types/spec-dialog";

// 2026-09-15-cb3e: the four chat commands as controls. "/spec" and "/spec new <project>"
// are a project picker and a button, and the caller's conversations are the list below —
// open and closed, each opened by clicking it. The strings still travel through the
// ingestion endpoint — the router parses them — but nobody has to type them.
//
// The dialog id is shown because it IS the session key: a page that lost it would open a
// second conversation beside one still running, and an operator comparing two tabs needs
// to see which is which.

export function DialogSessionControls({
  dialogId,
  view,
  conversations,
  picked,
  onPicked,
  onStartNew,
  onOpen,
}: {
  dialogId: string | null;
  view: SpecDialogView | null;
  conversations: SpecDialogSessionSummary[];
  picked: string;
  onPicked: (project: string) => void;
  onStartNew: (project?: string) => void;
  onOpen: (sessionId: string, openDialogId: string | null) => void;
}) {
  const projects = view?.projects ?? [];
  // Minting a dialog id and opening a session on it are two steps, and a second click
  // between them mints a second id: the first session opens on an id nothing is watching
  // while the page sits on the second with no session and no explanation. One click per
  // dialog id, released when the page is looking at one.
  const [starting, setStarting] = useState(false);
  useEffect(() => setStarting(false), [dialogId]);
  const here = view?.session?.sessionId;

  return (
    <div data-testid="dialog-controls" className="mb-3 flex flex-wrap items-center gap-2">
      <span data-testid="dialog-identity" className="dsh-label text-[var(--color-ink-mid)]">
        {view?.session
          ? `session ${view.session.sessionId} · dialog ${dialogId ?? ""}`
          : `dialog ${dialogId ?? ""} · no session open`}
      </span>
      {projects.length > 1 && (
        <select
          data-testid="dialog-project-picker"
          value={picked}
          onChange={(event) => onPicked(event.target.value)}
          className="rounded border border-stone-300 px-2 py-1 text-sm"
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
        className="rounded border border-stone-300 px-3 py-1 text-sm text-stone-700 hover:bg-stone-100 disabled:opacity-50"
      >
        New conversation
      </button>
      {conversations.length > 0 && (
        <ul data-testid="dialog-conversations" className="flex w-full flex-wrap gap-2">
          {conversations.map((conversation) => (
            <li key={conversation.sessionId}>
              <button
                type="button"
                data-testid={`dialog-conversation-${conversation.sessionId}`}
                aria-current={conversation.sessionId === here ? "true" : undefined}
                onClick={() => onOpen(conversation.sessionId, conversation.openDialogId)}
                className="rounded border border-stone-300 px-3 py-1 text-sm text-stone-700 hover:bg-stone-100 aria-[current=true]:bg-stone-100"
              >
                {label(conversation)}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function label(conversation: SpecDialogSessionSummary): string {
  const title = conversation.title ?? `untitled ${conversation.sessionId}`;
  return [title, conversation.project, outcome(conversation)].filter(Boolean).join(" · ");
}

function outcome({ outcome: filed }: SpecDialogSessionSummary): string | null {
  if (!filed) return null;
  const tickets = `${filed.tickets} ticket${filed.tickets === 1 ? "" : "s"} filed`;
  const what = filed.kind ? `${filed.kind}, ${tickets}` : tickets;
  return filed.partial ? `${what} (partial)` : what;
}
