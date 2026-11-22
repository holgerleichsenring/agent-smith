"use client";

import { useRef, type MutableRefObject, type ReactNode, type RefObject } from "react";
import type { Read } from "@/lib/tickets/TicketPageReads";
import { useTicketConversation, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketApprovalPreview } from "@/types/tickets";
import { TicketComposer } from "./TicketComposer";
import type { TicketReply } from "./ticketReply";

// 2026-10-17-e730a: the dock under the thread — the composer, or the note that the transcript is hidden. 2026-11-22-4321a:
// TicketTalk draws it once under Now or the Conversation section; `above` is the Conversation's Newest button. The preview is the
// section's own read, passed in: each useTicketApprovalPreview is a read of its own.

export interface ReplyFocus {
  input: RefObject<HTMLTextAreaElement | null>;
  composerReply: MutableRefObject<TicketReply | null>;
  /** The composer's reply — it sets a standing decision aside (2026-11-19-1b72c) — or the text field's focus. */
  reply: () => void;
}

export function useReplyFocus(): ReplyFocus {
  const input = useRef<HTMLTextAreaElement>(null);
  const composerReply = useRef<TicketReply | null>(null);
  const reply = () => (composerReply.current ? composerReply.current() : input.current?.focus());
  return { input, composerReply, reply };
}

interface Props {
  focus: ReplyFocus;
  preview: Read<TicketApprovalPreview>;
  dockRef?: RefObject<HTMLDivElement | null>;
  above?: ReactNode;
}

export function ConversationDock({ focus, preview, dockRef, above }: Props) {
  const detail = useTicketDetail();
  const data = useTicketConversation().data;
  return (
    <div className="conv-dock" ref={dockRef} data-testid="conv-dock">
      {above}
      {data && (
        <div className="conv-dock-body">
          {data.transcriptHidden ? (
            <div className="composer hidden-note" data-testid="conv-hidden">
              <div className="cp-head">
                <span className="sh-sub">The ticket conversation needs dialog.write: its turns and proposal are not shown.</span>
              </div>
            </div>
          ) : (
            <TicketComposer conversation={data} project={detail.project} ticketId={detail.ticketId}
              preview={preview} inputRef={focus.input} replyRef={focus.composerReply} />
          )}
        </div>
      )}
    </div>
  );
}
