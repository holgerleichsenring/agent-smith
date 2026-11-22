"use client";

import { useEffect, useRef, useState, type RefObject } from "react";
import type { Read } from "@/lib/tickets/TicketPageReads";
import { useTicketConversation, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketApprovalPreview } from "@/types/tickets";
import { causeEntryOf } from "./causeEntryMatch";
import { countItems, earlierWords, foldBefore, foldBoundary, NEWEST_SHOWN } from "./conversationFold";
import { ProposalBlock } from "./ProposalBlock";
import { ThreadItemList } from "./ThreadItemList";
import { threadItems } from "./threadItems";
import { TicketTextMessage } from "./ThreadMessages";
import { CAUSE_PARAM, useTicketTabGo } from "./useTicketTabGo";

// 2026-11-16-6877a: the conversation — the ticket first, every exchange and every attempt's report in order
// (threadItems), the open proposal; the composer is docked under it (TicketTalk). Carried over from the Conversation tab
// (2026-10-13-9a59r, 2026-10-17-e730a/b/f): it opens at its end unless ?causeOf= names an attempt, and "Newest" brings
// the reader back after scrolling up. 2026-11-17-8d1ca: it scrolls again each time ?causeOf= changes, so a "read"
// link on a page already showing the thread lands on its entry. Focusing the composer engages the ticket's session,
// so only a person's own act focuses it, never an address.
// 2026-11-22-4321a: the Conversation section. The Your move line, what the ticket waits for and the current run's
// log moved to Now; a report here is its line and body without the run log (Runs holds every run's log). The items
// before the first of the newest NEWEST_SHOWN at opening wait behind one toggle, outside the live log so unfolding
// them announces nothing; arrivals append below. A ?causeOf= naming an attempt shows everything and scrolls to that
// attempt's cause entry, or to its report line when no entry is its cause.

interface Props {
  preview: Read<TicketApprovalPreview>;
  endRef: RefObject<HTMLDivElement | null>;
}

export function TicketThread({ preview, endRef }: Props) {
  const detail = useTicketDetail();
  const conversation = useTicketConversation();
  const address = useTicketTabGo();
  const [unfolded, setUnfolded] = useState(false);
  const data = conversation.data;
  const causeOf = address.param(CAUSE_PARAM);
  const ticketUrl = detail.data?.summary.webUrl ?? null;
  const causeAttempt = causeOf ? detail.data?.attempts.find((a) => a.runId === causeOf) ?? null : null;
  const target = data && causeOf ? causeEntryOf(data.entries, data.attempts.find((a) => a.runId === causeOf)?.cause, ticketUrl) : undefined;

  const history = useRef<HTMLDivElement>(null);
  /** The ?causeOf= the page last scrolled for; undefined until the first scroll. */
  const scrolledFor = useRef<string | null | undefined>(undefined);
  /** The fold's boundary, fixed when the conversation is first read here. */
  const boundary = useRef<string | null | undefined>(undefined);
  const items = data && detail.data ? threadItems(data, detail.data) : [];
  if (data && detail.data && boundary.current === undefined) boundary.current = foldBoundary(items, NEWEST_SHOWN);

  useEffect(() => {
    if (!data || !detail.data || scrolledFor.current === causeOf) return;
    scrolledFor.current = causeOf;
    const selector = target ? `[data-entry-id="${cssEscape(target.id)}"]`
      : causeAttempt ? `[data-testid="thread-report-${causeAttempt.number}"]` : null;
    const node = selector ? history.current?.querySelector(selector) : null;
    if (node instanceof HTMLElement) node.scrollIntoView?.({ block: "center" });
    else endRef.current?.scrollIntoView({ block: "end" });
  }, [data, detail.data, target, causeAttempt, causeOf, endRef]);

  if (!detail.data) return null;
  const folded = foldBefore(items, boundary.current ?? null);
  // Unfolded by the toggle: the earlier items open above the log. Shown for a ?causeOf=: the log holds them all.
  const logItems = !unfolded && causeAttempt !== null ? items : folded.shown;
  const list = (part: typeof items) => <ThreadItemList items={part} conversation={data} target={target?.id ?? null} ticketUrl={ticketUrl} log={false} />;

  return (
    <div className="conv-history" ref={history}>
      <div className="thread" data-testid="conv-earlier-block">
        <TicketTextMessage />
        {!data && (
          <div className="msub" data-testid="conv-unread">
            {conversation.loading ? "Reading the conversation…" : "The conversation could not be read."}
          </div>
        )}
        {data?.threadUnavailable && (
          <div className="msub note" data-testid="conv-thread-unavailable">The ticket&apos;s own comments could not be read from its tracker.</div>
        )}
        {folded.earlier.length > 0 && causeAttempt === null && (
          <button type="button" className="tk-link tk-earlier" aria-expanded={unfolded} aria-controls="conv-earlier-items"
            onClick={() => setUnfolded(!unfolded)} data-testid="conv-earlier">
            {unfolded ? "Fold the earlier ones" : earlierWords(countItems(folded.earlier))}
          </button>
        )}
        <div id="conv-earlier-items">{unfolded && list(folded.earlier)}</div>
      </div>
      <div className="thread" role="log" aria-label="Conversation" data-testid="conv">
        {list(logItems)}
        {data?.proposal && !data.transcriptHidden && <ProposalBlock proposal={data.proposal} preview={preview.data} />}
      </div>
    </div>
  );
}

function cssEscape(value: string): string {
  return typeof CSS !== "undefined" && CSS.escape ? CSS.escape(value) : value.replace(/["\\]/g, "\\$&");
}
