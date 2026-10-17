"use client";

import { useEffect, useRef, useState } from "react";
import type { TicketConversation, TicketDetail } from "@/types/tickets";
import { causeEntryMatch, causeEntryOf } from "./causeEntryMatch";
import { ConversationEntry } from "./ConversationEntry";
import { CONVERSATION_FILTERS, attemptPill } from "./conversationWords";

// 2026-10-13-9a59r: the thread, grouped as 9a59h placed it — "before" the first attempt, under each attempt
// marker in turn, and "now" (after the last attempt's worked approval). The page does not regroup by time.
// One chip per 9a59h source filters it. ?causeOf={runId} scrolls once to that attempt's cause entry and marks it (e730b).

interface Props {
  conversation: TicketConversation;
  detail: TicketDetail;
  causeOf: string | null;
}

export function ConversationList({ conversation, detail, causeOf }: Props) {
  const [filter, setFilter] = useState("all");
  const source = CONVERSATION_FILTERS.find((f) => f.id === filter)?.source ?? null;
  const ticketUrl = detail.summary.webUrl ?? null;
  const number = (runId: string | null) => conversation.attempts.find((a) => a.runId === runId)?.number ?? null;
  const shown = conversation.entries.filter((e) => source === null || e.source === source);
  const startedBy = (entryId: string) => {
    const entry = conversation.entries.find((e) => e.id === entryId);
    const attempt = entry ? conversation.attempts.find((a) => causeEntryMatch(entry, a.cause, ticketUrl)) : undefined;
    return attempt?.number ?? null;
  };
  const groups = [
    { key: "before", head: "Before the first attempt", pill: null as [string, string] | null, runId: null as string | null },
    ...conversation.attempts.map((a) => ({
      key: a.runId, head: `Attempt ${a.number}`, runId: a.runId,
      pill: attemptPill(detail.attempts.find((d) => d.runId === a.runId)),
    })),
    { key: "now", head: "Now", pill: ["q", "in the ticket conversation"] as [string, string], runId: null },
  ];

  const target = causeOf
    ? causeEntryOf(conversation.entries, conversation.attempts.find((a) => a.runId === causeOf)?.cause, ticketUrl)?.id ?? null
    : null;
  const list = useRef<HTMLDivElement>(null);
  const scrolled = useRef<string | null>(null);
  useEffect(() => {
    if (!causeOf || scrolled.current === causeOf) return;
    scrolled.current = causeOf;
    const cause = conversation.attempts.find((a) => a.runId === causeOf)?.cause;
    const target = causeEntryOf(conversation.entries, cause, ticketUrl);
    if (!target) return;
    const node = list.current?.querySelector(`[data-entry-id="${cssEscape(target.id)}"]`);
    if (node instanceof HTMLElement) node.scrollIntoView?.({ block: "center" });
  }, [causeOf, conversation, ticketUrl]);

  return (
    <>
      <div className="conv-tools" role="group" aria-label="Filter">
        {CONVERSATION_FILTERS.map((f) => (
          <button key={f.id} type="button" className="chip" aria-pressed={f.id === filter}
            onClick={() => setFilter(f.id)} data-testid={`conv-filter-${f.id}`}>
            {f.label}
          </button>
        ))}
      </div>
      <div className="conv" ref={list} data-testid="conv">
        {groups.map((group) => {
          const entries = shown.filter((e) => (group.runId ? e.attemptRunId === group.runId : !e.attemptRunId && e.group === group.key));
          if (entries.length === 0 && group.runId === null) return null;
          return (
            <div key={group.key} className="c-group" data-testid={`conv-group-${group.key}`}>
              <div className="c-att">{group.head}{group.pill && <span className={`pill ${group.pill[0]}`}>{group.pill[1]}</span>}</div>
              {entries.map((e) => (
                <ConversationEntry key={e.id} entry={e} attemptNumber={number} started={startedBy(e.id)} target={e.id === target} />
              ))}
            </div>
          );
        })}
      </div>
    </>
  );
}

function cssEscape(value: string): string {
  return typeof CSS !== "undefined" && CSS.escape ? CSS.escape(value) : value.replace(/["\\]/g, "\\$&");
}
