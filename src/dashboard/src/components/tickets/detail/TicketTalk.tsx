"use client";

import { useRef, type ReactNode, type RefObject } from "react";
import { useTicketApprovalPreview, useTicketConversation } from "@/lib/tickets/TicketPageStore";
import { ConversationDock, useReplyFocus } from "./ConversationDock";
import { TicketReplyContext } from "./ticketReply";
import { TicketNow } from "./TicketNow";
import { TicketThread } from "./TicketThread";
import { useNewestJump } from "./useNewestJump";

// 2026-11-22-4321a: Now and the Conversation section around ONE composer. The section is the first child and the dock
// keeps its place after it, so switching between the two keeps the composer mounted — a draft, a queued send and a
// decision set aside for chatting stay. The reply focus, the approval preview (one read) and the end anchor belong
// here and are handed to the section; the Newest button shows only under the conversation.

interface Props {
  conversation: boolean;
  /** What Now draws under its Your move line: what the ticket waits for and the divergence notice. */
  lead?: ReactNode;
}

export function TicketTalk({ conversation, lead }: Props) {
  const focus = useReplyFocus();
  const preview = useTicketApprovalPreview();
  const sentinel = useRef<HTMLDivElement>(null);
  const dock = useRef<HTMLDivElement>(null);
  const end = useRef<HTMLDivElement>(null);
  return (
    <TicketReplyContext.Provider value={focus.reply}>
      {conversation
        ? <TicketThread preview={preview} endRef={end} />
        : <TicketNow lead={lead} focus={focus} preview={preview} />}
      <div ref={sentinel} data-testid="conv-sentinel" />
      <ConversationDock focus={focus} preview={preview} dockRef={dock}
        above={conversation ? <NewestJumpButton sentinel={sentinel} dock={dock} end={end} /> : null} />
      <div ref={end} data-testid="conv-end" />
    </TicketReplyContext.Provider>
  );
}

interface JumpProps {
  sentinel: RefObject<HTMLDivElement | null>;
  dock: RefObject<HTMLDivElement | null>;
  end: RefObject<HTMLDivElement | null>;
}

/** 2026-10-17-e730f: "↓ Newest" while the reader is away from the conversation's end, with how many entries arrived. */
function NewestJumpButton({ sentinel, dock, end }: JumpProps) {
  const entries = useTicketConversation().data?.entries ?? null;
  const jump = useNewestJump(entries, sentinel, dock, end);
  if (!jump.away) return null;
  return (
    <button type="button" className="chip conv-newest" onClick={jump.jump} data-testid="conv-newest">
      ↓ Newest{jump.count > 0 ? ` · ${jump.count} new` : ""}
    </button>
  );
}
