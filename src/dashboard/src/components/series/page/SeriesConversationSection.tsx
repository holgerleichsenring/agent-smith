"use client";

import { useMemo } from "react";
import Link from "next/link";
import { useT } from "@/hooks/useT";
import { useSpecDialog } from "@/hooks/useSpecDialog";
import type { DialogIdSource } from "@/lib/specDialogSession";
import { conversationHref } from "@/components/dialog/conversationHref";
import { DialogComposer } from "@/components/dialog/DialogComposer";
import { DialogTranscript } from "@/components/dialog/DialogTranscript";
import { DialogWorking } from "@/components/dialog/DialogWorking";
import { isOpenApproval } from "@/components/tickets/detail/useTicketComposer";
import type { SeriesRunDetail } from "@/types/seriesRuns";
import "@/styles/spec-dialog.css";
import { SeriesQuestionBlock } from "./SeriesQuestionBlock";
import { useChatWindow } from "./useChatWindow";

// 2026-11-23-31dcd: the conversation that filed the series, with Work it out's own hook, transcript and composer. The
// hook starts ON the conversation's thread (a source that seeds that dialog id and never holds it), so the browser's
// held Work it out id is never touched and viewing reads and writes nothing. A closed conversation is continued in Work
// it out, where resuming is a person's act; a proposal waiting for a decision is decided there too, where it is shown.
// 2026-11-25-9992j: this is the series page's chat, its only input; the series' waiting question sits above the composer.
// 2026-11-26-5201a: the transcript is a window that scrolls by itself — the newest entries, opened at the bottom, older
// ones rendered as the reader scrolls up; the working line scrolls with it, the question and the composer stay below.
// 2026-11-26-5201c: the questions that wait are one block pinned above the input (SeriesQuestionBlock), a closed
// conversation's too, where they show without buttons.

/** The dialog id source of one thread: it starts there and stays there. */
export function threadSource(threadId: string): DialogIdSource {
  return { seed: () => threadId, switchTo: (to) => to ?? threadId };
}

export function SeriesConversationSection({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  if (!detail.threadId) {
    return (
      <>
        <SeriesQuestionBlock detail={detail} threadId={null} onAnswer={null} />
        <p className="msub" data-testid="series-talk-gone">{t("series.conversation.gone")}</p>
      </>
    );
  }
  if (!detail.conversationOpen) {
    return (
      <>
        <SeriesQuestionBlock detail={detail} threadId={null} onAnswer={null} />
        <p className="msub" data-testid="series-talk-closed">
          {t("series.conversation.closed")}{" "}
          <Link href={conversationHref({ sessionId: detail.conversation, openDialogId: null })}>{t("series.conversation.continue")}</Link>
        </p>
      </>
    );
  }
  return <OpenConversation detail={detail} threadId={detail.threadId} />;
}

function OpenConversation({ detail, threadId }: { detail: SeriesRunDetail; threadId: string }) {
  const t = useT();
  const source = useMemo(() => threadSource(threadId), [threadId]);
  const dialog = useSpecDialog(source);
  const there = dialog.view?.session?.sessionId === detail.conversation;
  const deciding = dialog.awaitingProposal !== null || isOpenApproval(dialog.question);
  const chat = useChatWindow(dialog.entries.length);
  return (
    <div className="mock-dialog sr-talk" data-testid="series-talk">
      <div ref={chat.box} className="sr-scroll" onScroll={chat.onScroll} data-testid="series-chat-scroll">
        <div ref={chat.content} className="sr-scroll-body">
          {chat.from > 0 && (
            <button type="button" className="d-link sr-earlier" onClick={chat.more} data-testid="series-chat-earlier">
              {t("series.chat.earlier", { count: chat.from })}
            </button>
          )}
          <DialogTranscript entries={dialog.entries} from={chat.from} />
          {dialog.working && <DialogWorking readings={dialog.readings} activity={dialog.activity} since={dialog.workingSince} />}
        </div>
      </div>
      {dialog.failure && <p className="msub" role="alert" data-testid="series-talk-failed">{t("series.conversation.failed", { message: dialog.failure.message })}</p>}
      <SeriesQuestionBlock detail={detail} threadId={threadId} failed={!!dialog.failure} deciding={deciding}
        onAnswer={there && !deciding ? (answer) => void dialog.send(answer, detail.project) : null} />
      {deciding ? (
        <p className="msub" data-testid="series-talk-decide">
          {t("series.conversation.decide")}{" "}
          <Link href={conversationHref({ sessionId: detail.conversation, openDialogId: threadId })}>{t("series.conversation.continue")}</Link>
        </p>
      ) : (
        <DialogComposer disabled={!there} onSend={(text) => void dialog.send(text, detail.project)}
          onAttach={(file) => dialog.attach(file, detail.project)} note={dialog.uploadNote} />
      )}
    </div>
  );
}
