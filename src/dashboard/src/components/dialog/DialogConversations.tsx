"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import type { SpecDialogSessionSummary } from "@/types/spec-dialog";
import { lastActive, outcomeLabel } from "./conversationRows";

// 2026-09-15-cb3e: "/spec" and "/spec new <project>" are a project picker and a button, and
// the caller's conversations are a list — open and closed, each opened by clicking it.
// 2026-09-22-2a86: and the strings stopped travelling. The button mints a tab the first typed
// message opens the conversation on, carrying the picked project; a row calls the resume ROUTE.
// Nothing here posts text for the server to parse back into a command.
// 2026-09-17-c7aed: the list is the left column, grouped by day and marked with what each
// conversation filed.
// 2026-09-17-042ef: the column is the Projects page's panel card, the day is its field label
// and the marks under a conversation are its marks — the local chip that drew them is gone.
// 2026-09-18-7a05: a row offers a delete, so the ROW stops being a button — a control nested in
// a button is invalid markup and warns. The row is a container now, with the open control and
// the delete control as siblings; neither is inside the other, and neither has to stop a click
// travelling out of it.
// 2026-09-23-6e3f: the project select is gone from here. It is asked once, in the exchange
// column's empty state, where a person starting a conversation is already looking — two places
// to pick one thing is how a select at the top of this column came to sit half a page away from
// the box it was silently disabling.

export function DialogConversations({
  dialogId,
  sessionHere,
  conversations,
  onStartNew,
  onOpen,
  onDelete,
}: {
  dialogId: string | null;
  sessionHere: string | null;
  conversations: SpecDialogSessionSummary[];
  onStartNew: () => void;
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
        {/* 2026-09-21-f237c: ONE heading. The card already named the list, and adding a Recents
            heading under it would have put two names on one thing. Recents is the truer name:
            the card holds the recent ones, the way to start another, and the link to all of
            them. */}
        <h2 className="ec-name sans">Recents</h2>
      </div>
      <div className="d-body flex flex-col gap-3">
        <button
          type="button"
          data-testid="dialog-new"
          disabled={starting}
          onClick={() => {
            setStarting(true);
            onStartNew();
          }}
          className="btn primary w-full"
        >
          + New conversation
        </button>
        {conversations.length > 0 && (
          <div data-testid="dialog-conversations" className="flex flex-col gap-0.5">
            {drawn(conversations, sessionHere).map((conversation) => (
              <Conversation
                key={conversation.sessionId}
                conversation={conversation}
                current={conversation.sessionId === sessionHere}
                onOpen={onOpen}
                onDelete={onDelete}
              />
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

/** How many rows the panel draws. Not how many it HOLDS — see `drawn`. */
export const DRAWN = 20;

/**
 * 2026-09-21-f237c: the rows to draw — the first twenty, plus the conversation open here when
 * the list holds it and the twenty do not.
 *
 * The cap is taken HERE, on the way to the screen, and never on the array behind it. The hook
 * finds the conversation open here in that same array to decide whether the list is behind, and
 * the surface reads that conversation's fallback heading from it; a sliced array would make a
 * conversation below the twentieth unfindable in both, and the predicate would then answer
 * "behind" on every reply for ever. Twenty is how many a person scans; the fifty the server
 * serves is how far back the page can still recognise what it has open. Two numbers, two
 * questions.
 *
 * The open one is appended rather than left out because today every row is drawn, so the row
 * carrying aria-current is always on screen — and 2026-09-21-f237b made a twenty-first
 * conversation very reachable. A panel that does not contain what the reader is reading is worse
 * than one row too many.
 */
export function drawn(
  conversations: SpecDialogSessionSummary[],
  sessionHere: string | null,
): SpecDialogSessionSummary[] {
  const rows = conversations.slice(0, DRAWN);
  if (sessionHere === null || rows.some((row) => row.sessionId === sessionHere)) return rows;
  const open = conversations.find((row) => row.sessionId === sessionHere);
  return open ? [...rows, open] : rows;
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
  const time = lastActive(conversation.lastActivityAt);
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
        {/* 2026-09-21-f237c: two lines, then the ellipsis — the same truncation, one line
            later. `block` comes off because line-clamp sets its own display. */}
        <span className="line-clamp-2 dsh-body font-medium text-ink">{title}</span>
        <span className="ec-marks ec-sub items-center">
          <span className="ec-mark given">{conversation.project}</span>
          <span>
            {conversation.turns} turn{conversation.turns === 1 ? "" : "s"}
          </span>
          <span>{time}</span>
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
