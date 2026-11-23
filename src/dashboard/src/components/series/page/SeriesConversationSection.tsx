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

// 2026-11-23-31dcd: the conversation that filed the series, with Work it out's own hook, transcript and composer. The
// hook starts ON the conversation's thread (a source that seeds that dialog id and never holds it), so the browser's
// held Work it out id is never touched and viewing reads and writes nothing. A closed conversation is continued in Work
// it out, where resuming is a person's act; a proposal waiting for a decision is decided there too, where it is shown.

/** The dialog id source of one thread: it starts there and stays there. */
export function threadSource(threadId: string): DialogIdSource {
  return { seed: () => threadId, switchTo: (to) => to ?? threadId };
}

export function SeriesConversationSection({ detail }: { detail: SeriesRunDetail }) {
  const t = useT();
  if (!detail.threadId) return <p className="msub" data-testid="series-talk-gone">{t("series.conversation.gone")}</p>;
  if (!detail.conversationOpen) {
    return (
      <p className="msub" data-testid="series-talk-closed">
        {t("series.conversation.closed")}{" "}
        <Link href={conversationHref({ sessionId: detail.conversation, openDialogId: null })}>{t("series.conversation.continue")}</Link>
      </p>
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
  return (
    <div className="mock-dialog sr-talk" data-testid="series-talk">
      <p className="msub">{t("series.conversation.sub")}</p>
      <DialogTranscript entries={dialog.entries} />
      {dialog.working && <DialogWorking readings={dialog.readings} activity={dialog.activity} since={dialog.workingSince} />}
      {dialog.failure && <p className="msub" role="alert" data-testid="series-talk-failed">{t("series.conversation.failed", { message: dialog.failure.message })}</p>}
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
