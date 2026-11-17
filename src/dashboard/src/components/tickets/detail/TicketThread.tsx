"use client";

import { useEffect, useRef, type ReactNode } from "react";
import { useTicketApprovalPreview, useTicketConversation, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketConversation, TicketConversationEntry } from "@/types/tickets";
import { causeEntryMatch, causeEntryOf } from "./causeEntryMatch";
import { ConversationEntry } from "./ConversationEntry";
import { ProposalBlock } from "./ProposalBlock";
import { RetryUnchanged } from "./RetryUnchanged";
import { TicketComposer } from "./TicketComposer";
import { threadItems, type ThreadItem } from "./threadItems";
import { AttemptReport, DerivationMessage, RepeatLine, TicketTextMessage } from "./ThreadMessages";
import { useNewestJump } from "./useNewestJump";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";
import { YourMoveBox } from "./YourMoveBox";

// 2026-11-16-6877a: the ticket page's body — the conversation, leading. The ticket first, every exchange and every
// attempt's report in order (threadItems), the open proposal, then whatever `after` holds (the Details fold); the
// dock at the bottom holds the Your move box above the composer. Carried over from the Conversation tab
// (2026-10-13-9a59r, 2026-10-17-e730a/b/f): it opens at its end unless a question waits or ?causeOf= names an entry,
// and "Newest" brings the reader back after scrolling up. Focusing the composer engages the ticket's session, so
// only a person's own act focuses it, never an address.

export function TicketThread({ after }: { after?: ReactNode }) {
  const detail = useTicketDetail();
  const conversation = useTicketConversation();
  const preview = useTicketApprovalPreview();
  const address = useTicketTabGo();
  const attempts = detail.data?.attempts ?? [];
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const asking = [...attempts].reverse().find((a) => a.pendingQuestion) ?? null;
  const data = conversation.data;
  const causeOf = address.param(CAUSE_PARAM);
  const ticketUrl = detail.data?.summary.webUrl ?? null;
  const target = data && causeOf ? causeEntryOf(data.entries, data.attempts.find((a) => a.runId === causeOf)?.cause, ticketUrl) : undefined;

  const question = useRef<HTMLDivElement>(null);
  const end = useRef<HTMLDivElement>(null);
  const list = useRef<HTMLDivElement>(null);
  const opened = useRef(false);
  const sentinel = useRef<HTMLDivElement>(null);
  const dock = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLTextAreaElement>(null);
  const newestJump = useNewestJump(data?.entries ?? null, sentinel, dock, end);
  useEffect(() => {
    if (!data || !detail.data || opened.current) return;
    opened.current = true;
    if (target) {
      const node = list.current?.querySelector(`[data-entry-id="${cssEscape(target.id)}"]`);
      if (node instanceof HTMLElement) node.scrollIntoView?.({ block: "center" });
      return;
    }
    if (asking && question.current) question.current.scrollIntoView({ block: "start" });
    else end.current?.scrollIntoView({ block: "end" });
  }, [data, detail.data, target, asking]);

  if (!detail.data) return null;
  const reply = () => input.current?.focus();
  const answer = () => question.current?.scrollIntoView?.({ block: "center" });

  return (
    <>
      <div className="thread" role="log" aria-label="Conversation" ref={list} data-testid="conv">
        <TicketTextMessage />
        {!data && (
          <div className="msub" data-testid="conv-unread">
            {conversation.loading ? "Reading the conversation…" : "The conversation could not be read."}
          </div>
        )}
        {data?.threadUnavailable && (
          <div className="msub note" data-testid="conv-thread-unavailable">The ticket&apos;s own comments could not be read from its tracker.</div>
        )}
        <Items items={threadItems(data, detail.data)} conversation={data} target={target?.id ?? null}
          ticketUrl={ticketUrl} askingRunId={asking?.runId ?? null} questionRef={question} />
        {data?.proposal && !data.transcriptHidden && <ProposalBlock proposal={data.proposal} preview={preview.data} />}
      </div>
      <div ref={sentinel} data-testid="conv-sentinel" />
      {after}
      <div className="conv-dock" ref={dock} data-testid="conv-dock">
        {newestJump.away && (
          <button type="button" className="chip conv-newest" onClick={newestJump.jump} data-testid="conv-newest">
            ↓ Newest{newestJump.count > 0 ? ` · ${newestJump.count} new` : ""}
          </button>
        )}
        <YourMoveBox detail={detail.data} onReply={reply} onAnswer={answer} />
        {data && (
          <div className="conv-dock-body">
            {data.transcriptHidden ? (
              <div className="composer hidden-note" data-testid="conv-hidden">
                <div className="cp-head">
                  <span className="sh-sub">The ticket conversation needs dialog.write: its turns and proposal are not shown.</span>
                </div>
                <div className="cp-actions"><RetryUnchanged attempt={newest} project={detail.project} /></div>
              </div>
            ) : (
              <TicketComposer conversation={data} project={detail.project} ticketId={detail.ticketId}
                preview={preview} newest={newest} inputRef={input} />
            )}
          </div>
        )}
      </div>
      <div ref={end} data-testid="conv-end" />
    </>
  );
}

interface ItemsProps {
  items: ThreadItem[];
  conversation: TicketConversation | null;
  target: string | null;
  ticketUrl: string | null;
  askingRunId: string | null;
  questionRef: React.RefObject<HTMLDivElement | null>;
}

function Items({ items, conversation, target, ticketUrl, askingRunId, questionRef }: ItemsProps) {
  const number = (runId: string | null) => conversation?.attempts.find((a) => a.runId === runId)?.number ?? null;
  const startedBy = (entry: TicketConversationEntry) =>
    conversation?.attempts.find((a) => causeEntryMatch(entry, a.cause, ticketUrl))?.number ?? null;
  return (
    <>
      {items.map((item) => {
        switch (item.kind) {
          case "entry":
            return <ConversationEntry key={item.entry.id} entry={item.entry} attemptNumber={number}
              started={startedBy(item.entry)} target={item.entry.id === target} />;
          case "report":
            return <AttemptReport key={`report-${item.attempt.runId}`} attempt={item.attempt}
              questionRef={item.attempt.runId === askingRunId ? questionRef : undefined} />;
          case "derivation":
            return <DerivationMessage key={`derivation-${item.derivation.runId}`} derivation={item.derivation} />;
          case "repeat":
            return (
              <RepeatLine key={item.key} line={item.line} testId={`thread-repeat-${item.key}`}>
                <Items items={item.items} conversation={conversation} target={target} ticketUrl={ticketUrl}
                  askingRunId={askingRunId} questionRef={questionRef} />
              </RepeatLine>
            );
        }
      })}
    </>
  );
}

function cssEscape(value: string): string {
  return typeof CSS !== "undefined" && CSS.escape ? CSS.escape(value) : value.replace(/["\\]/g, "\\$&");
}
