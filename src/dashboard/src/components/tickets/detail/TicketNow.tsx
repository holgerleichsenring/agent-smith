"use client";

import { useEffect, useRef, type ReactNode } from "react";
import Link from "next/link";
import type { Read } from "@/lib/tickets/TicketPageReads";
import { useTicketConversation, useTicketDetail } from "@/lib/tickets/TicketPageStore";
import type { TicketApprovalPreview, TicketAttempt, TicketDetail } from "@/types/tickets";
import { latestEntry, wholeConversationWords } from "./conversationFold";
import type { ReplyFocus } from "./ConversationDock";
import { nowRun } from "./nowRun";
import { ProposalBlock } from "./ProposalBlock";
import { retriable, RetryUnchanged } from "./RetryUnchanged";
import { runListLines } from "./runLines";
import { ThreadItemList } from "./ThreadItemList";
import { threadItems } from "./threadItems";
import { AttemptReport, DerivationMessage } from "./ThreadMessages";
import { useTicketTabGo } from "./useTicketTabGo";
import { useYourMove } from "./useYourMove";
import { YourMoveBox } from "./YourMoveBox";

// 2026-11-22-4321a: Now, the ticket page's first section — the Your move line, `lead` (what the ticket waits for and
// what waits for it), the current run opened with its log (nowRun), the newest conversation entry only, the open
// proposal and the way to the whole conversation; TicketTalk puts the composer under it. The current run is above
// every entry, so it is in view however long the conversation has grown. It opens at its top, or at the question a
// run waits on. Retry unchanged is always on the newest attempt: in its report when that is the current run, else on
// its own line — unless the Your move box's one act already is it. A ticket with no coding run says so in one line.

interface Props {
  lead?: ReactNode;
  focus: ReplyFocus;
  preview: Read<TicketApprovalPreview>;
}

export function TicketNow({ lead, focus, preview }: Props) {
  const detail = useTicketDetail();
  const conversation = useTicketConversation();
  const address = useTicketTabGo();
  const question = useRef<HTMLDivElement>(null);
  const retryInBox = useYourMove(detail.data ?? null)?.move.act?.kind === "retry";
  const current = detail.data ? nowRun(detail.data) : { attempt: null, derivation: null };
  const asking = current.attempt?.pendingQuestion ? current.attempt : null;
  const scrolled = useRef(false);
  useEffect(() => {
    if (scrolled.current || !asking || !question.current) return;
    scrolled.current = true;
    question.current.scrollIntoView?.({ block: "start" });
  }, [asking]);

  if (!detail.data) return null;
  const data = conversation.data;
  const attempts = detail.data.attempts;
  const newest = attempts.length > 0 ? attempts[attempts.length - 1] : null;
  const retryNewest = !retryInBox && newest !== null;
  const entry = data ? latestEntry(threadItems(data, detail.data)) : null;
  const answer = () => question.current?.scrollIntoView?.({ block: "center" });
  return (
    <>
      <YourMoveBox detail={detail.data} onReply={focus.reply} onAnswer={answer} />
      {lead}
      {current.attempt || current.derivation ? (
        <div className="thread" data-testid="now-run">
          {current.attempt && (
            <AttemptReport key={current.attempt.runId} attempt={current.attempt} initiallyOpen log
              retry={retryNewest && current.attempt.runId === newest?.runId} questionRef={asking ? question : undefined} />
          )}
          {current.derivation && <DerivationMessage derivation={current.derivation} />}
        </div>
      ) : (
        <NoCodingRun detail={detail.data} />
      )}
      {retryNewest && newest && current.attempt && current.attempt.runId !== newest.runId && (
        <NewestRetry attempt={newest} project={detail.project} />
      )}
      <div className="thread" data-testid="now-latest">
        {!data && (
          <div className="msub" data-testid="conv-unread">
            {conversation.loading ? "Reading the conversation…" : "The conversation could not be read."}
          </div>
        )}
        {entry && <ThreadItemList items={[{ kind: "entry", entry }]} conversation={data} target={null}
          ticketUrl={detail.data.summary.webUrl ?? null} log={false} />}
        {data?.proposal && !data.transcriptHidden && <ProposalBlock proposal={data.proposal} preview={preview.data} />}
      </div>
      {data && (
        <button type="button" className="tk-link tk-now-more" onClick={() => address.go("conversation")}
          data-testid="now-conversation-link">
          {wholeConversationWords(data.entries.length)}
        </button>
      )}
    </>
  );
}

/** The newest attempt's Retry when an older attempt's question is the current run. */
function NewestRetry({ attempt, project }: { attempt: TicketAttempt; project: string }) {
  if (!retriable(attempt)) return null;
  return (
    <div className="cp-actions tk-now-retry" data-testid={`thread-retry-${attempt.number}`}>
      <RetryUnchanged attempt={attempt} project={project} />
    </div>
  );
}

/** A ticket whose runs are not coding runs (a deploy, a merge): one line and its newest run. */
function NoCodingRun({ detail }: { detail: TicketDetail }) {
  const newest = runListLines(detail.attempts, detail.derivations ?? [], detail.otherRuns ?? [])[0] ?? null;
  return (
    <div className="msub" data-testid="now-no-run">
      No coding run yet.
      {newest && <> Newest run: <Link href={newest.href} data-testid="now-newest-run">{newest.who} · {newest.outcome}</Link></>}
    </div>
  );
}
