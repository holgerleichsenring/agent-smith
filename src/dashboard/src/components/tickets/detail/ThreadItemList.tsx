"use client";

import type { RefObject } from "react";
import type { TicketConversation, TicketConversationEntry } from "@/types/tickets";
import { causeEntryMatch } from "./causeEntryMatch";
import { ConversationEntry } from "./ConversationEntry";
import { AttemptReport, DerivationMessage, RepeatLine } from "./ThreadMessages";
import type { ThreadItem } from "./threadItems";

// 2026-11-16-6877a: the thread's items, each drawn as its line — an entry, an attempt's report, a derivation, a
// folded repeat holding its own items. 2026-11-22-4321a: moved out of the thread, so Now draws its latest entry and
// the Conversation section its items with one component; `log` says whether a report's body holds the run log.

export interface ThreadItemListProps {
  items: ThreadItem[];
  conversation: TicketConversation | null;
  target: string | null;
  ticketUrl: string | null;
  askingRunId?: string | null;
  /** The attempt whose line opens by itself. */
  openRunId?: string | null;
  /** 2026-11-18-d6d2d: the attempt whose report offers Retry unchanged. */
  retryRunId?: string | null;
  questionRef?: RefObject<HTMLDivElement | null>;
  log: boolean;
}

export function ThreadItemList(props: ThreadItemListProps) {
  const { items, conversation, target, ticketUrl, askingRunId, openRunId, retryRunId, questionRef, log } = props;
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
            return <AttemptReport key={`report-${item.attempt.runId}`} attempt={item.attempt} log={log}
              initiallyOpen={item.attempt.runId === openRunId} retry={item.attempt.runId === retryRunId}
              questionRef={item.attempt.runId === askingRunId ? questionRef : undefined} />;
          case "derivation":
            return <DerivationMessage key={`derivation-${item.derivation.runId}`} derivation={item.derivation} />;
          case "repeat":
            return (
              <RepeatLine key={item.key} items={item.items} testId={`thread-repeat-${item.key}`}>
                <ThreadItemList {...props} items={item.items} />
              </RepeatLine>
            );
        }
      })}
    </>
  );
}
