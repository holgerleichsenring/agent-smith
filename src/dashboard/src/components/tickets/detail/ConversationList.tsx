"use client";

import { useEffect, useRef, useState } from "react";
import type { TicketConversation, TicketDetail } from "@/types/tickets";
import { causeEntryMatch, causeEntryOf } from "./causeEntryMatch";
import { ConversationEntry } from "./ConversationEntry";
import { conversationGroups, entryCount, type ConversationGroup } from "./conversationGroups";
import { CONVERSATION_FILTERS } from "./conversationWords";

// 2026-10-13-9a59r: the thread, grouped as 9a59h placed it — "before" the first attempt, under each attempt
// marker in turn, and "now" (after the last attempt's worked approval). The page does not regroup by time.
// One chip per 9a59h source filters it. 2026-10-17-e730c: finished attempts fold to one line (conversationGroups). ?causeOf={runId} scrolls once to that attempt's cause entry and marks it (e730b).

interface Props {
  conversation: TicketConversation;
  detail: TicketDetail;
  causeOf: string | null;
}

export function ConversationList({ conversation, detail, causeOf }: Props) {
  const [filter, setFilter] = useState("all");
  // 2026-10-17-e730c: the reader's own folds, cleared whenever the filter changes.
  const [toggles, setToggles] = useState<Record<string, boolean>>({});
  const source = CONVERSATION_FILTERS.find((f) => f.id === filter)?.source ?? null;
  const ticketUrl = detail.summary.webUrl ?? null;
  const number = (runId: string | null) => conversation.attempts.find((a) => a.runId === runId)?.number ?? null;
  const startedBy = (entryId: string) => {
    const entry = conversation.entries.find((e) => e.id === entryId);
    const attempt = entry ? conversation.attempts.find((a) => causeEntryMatch(entry, a.cause, ticketUrl)) : undefined;
    return attempt?.number ?? null;
  };
  const target = causeOf
    ? causeEntryOf(conversation.entries, conversation.attempts.find((a) => a.runId === causeOf)?.cause, ticketUrl)?.id ?? null
    : null;
  const groups = conversationGroups(conversation, detail, source, target);
  const choose = (id: string) => {
    setFilter(id);
    setToggles({});
  };

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
            onClick={() => choose(f.id)} data-testid={`conv-filter-${f.id}`}>
            {f.label}
          </button>
        ))}
      </div>
      <div className="conv" ref={list} data-testid="conv">
        {groups.map((group) => {
          const open = !group.collapsible || (toggles[group.key] ?? group.openByDefault);
          return (
            <div key={group.key} className="c-group" data-testid={`conv-group-${group.key}`}>
              <GroupHead group={group} open={open}
                onToggle={() => setToggles((t) => ({ ...t, [group.key]: !open }))} />
              {open && group.entries.map((e) => (
                <ConversationEntry key={e.id} entry={e} attemptNumber={number} started={startedBy(e.id)} target={e.id === target} />
              ))}
            </div>
          );
        })}
      </div>
    </>
  );
}

/** An attempt's marker: open, its name and outcome; folded, one line adding the end cause and the entry count. */
function GroupHead({ group, open, onToggle }: { group: ConversationGroup; open: boolean; onToggle: () => void }) {
  const pill = group.pill && <span className={`pill ${group.pill[0]}`}>{group.pill[1]}</span>;
  if (!group.collapsible) return <div className="c-att">{group.head}{pill}</div>;
  const line = (
    <>
      <span>{group.head}</span>
      {pill}
      {!open && group.cause && <span className="c-cause" title={group.cause}>· {group.cause}</span>}
      {!open && <span className="c-count">· {entryCount(group.entries.length)}</span>}
    </>
  );
  if (group.entries.length === 0) {
    return <div className="c-att"><span className="c-toggle" data-testid={`conv-line-${group.key}`}>{line}</span></div>;
  }
  return (
    <div className="c-att">
      <button type="button" className="c-toggle" aria-expanded={open} onClick={onToggle} data-testid={`conv-toggle-${group.key}`}>
        <span aria-hidden="true">{open ? "\u25BE" : "\u25B8"}</span>
        {line}
      </button>
    </div>
  );
}

function cssEscape(value: string): string {
  return typeof CSS !== "undefined" && CSS.escape ? CSS.escape(value) : value.replace(/["\\]/g, "\\$&");
}
