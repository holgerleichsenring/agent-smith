"use client";

import { useT } from "@/hooks/useT";
import { projectLabel } from "@/lib/installation";
import { useCallback, useEffect, useState } from "react";
import Link from "next/link";

import type { SpecDialogSessionSummary } from "@/types/spec-dialog";
import { refusalIn } from "@/lib/apiResponse";
import {
  deleteSpecDialogConversation,
  fetchSpecDialogConversations,
} from "@/lib/specDialogApi";
import { RefusalSurface } from "@/components/shell/RefusalSurface";
import { PageHead } from "@/components/system/PageHead";
import { ConfirmDialog, useConfirmDialog } from "./ConfirmDialog";
import { conversationHref } from "./conversationHref";
import { deletionWarning } from "./conversationDelete";
import { lastActive, outcomeLabel } from "./conversationRows";

// 2026-09-21-f237b: every conversation the caller holds, with room to read one.
//
// Until this page the panel beside the exchange was the ONLY place a past conversation could be
// reached — no page, no search, no address — so one that had scrolled out of it was gone as far
// as the operator was concerned. The rows here are the panel's rows with the space the panel
// does not have, and they carry the delete the panel alone used to carry, which is what lets
// 2026-09-21-f237c bound the panel to twenty.
//
// It asks for the CEILING rather than the server's default: the point of the page is the rows
// the panel does not show. What it cannot promise is "all" — the cap is taken by id while the
// order is by last activity, so a conversation started long ago and resumed today can fall
// outside any limit. The page therefore counts out loud and says "the most recent", never "all".

/** What the page asks the list for. The server clamps to its own ceiling either way. */
export const PAGE_LIMIT = 200;

export function ConversationsView() {
  const [rows, setRows] = useState<SpecDialogSessionSummary[] | null>(null);
  const [total, setTotal] = useState(0);
  const [failure, setFailure] = useState<Error | null>(null);
  const t = useT();
  const confirmation = useConfirmDialog();

  const load = useCallback(async (signal?: AbortSignal) => {
    try {
      const page = await fetchSpecDialogConversations(PAGE_LIMIT, signal);
      setRows(page.conversations);
      setTotal(page.total);
    } catch (thrown) {
      if (signal?.aborted) return;
      setFailure(thrown instanceof Error ? thrown : new Error(String(thrown)));
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  // The same question the panel asks, from the same rule, so a conversation is not easier to
  // lose from here than from there.
  const remove = useCallback(async (conversation: SpecDialogSessionSummary) => {
    const warning = deletionWarning(t, conversation);
    if (warning !== null && !(await confirmation.ask(warning, { confirmLabel: t("common.delete") }))) return;
    try {
      await deleteSpecDialogConversation(conversation.sessionId);
    } catch (thrown) {
      setFailure(thrown instanceof Error ? thrown : new Error(String(thrown)));
      return;
    }
    setRows((held) => (held ?? []).filter((row) => row.sessionId !== conversation.sessionId));
    setTotal((held) => Math.max(0, held - 1));
  }, [confirmation, t]);

  const refusal = refusalIn(failure);

  return (
    <div className="mock-shell mock-dialog" data-testid="conversations-page">
      <main className="main">
        <PageHead
          title={t("dialog.conversations.label")}
          sub={t("dialog.conversations.sub")}
        />
        {/* A refusal reads as one: the list route needs the dialog permission, and the rail
            offers Work it out to every caller, so a caller without it must be told which
            permission is missing rather than shown a page that looks like an empty history. */}
        {refusal
          ? <RefusalSurface refusal={refusal} surface="Conversations" />
          : <Listed rows={rows} total={total} failure={failure} onDelete={remove} />}
      </main>
      <ConfirmDialog {...confirmation.dialog} />
    </div>
  );
}

function Listed({
  rows,
  total,
  failure,
  onDelete,
}: {
  rows: SpecDialogSessionSummary[] | null;
  total: number;
  failure: Error | null;
  onDelete: (conversation: SpecDialogSessionSummary) => void;
}) {
  const t = useT();
  if (failure) {
    return (
      <p className="ec-sub" role="alert" data-testid="conversations-failed">
        {t("dialog.conversations.failed", { reason: failure.message })}
      </p>
    );
  }
  if (rows === null) return <p className="ec-sub" data-testid="conversations-loading">{t("dialog.conversations.reading")}</p>;
  if (rows.length === 0) {
    return (
      <p className="ec-sub" data-testid="conversations-none">
        {t("dialog.conversations.none")}
      </p>
    );
  }
  return (
    <section className="ecard inert">
      <div className="d-head">
        <div className="d-head-t">
          <h2 className="ec-name sans">{t("dialog.conversations.label")}</h2>
          {/* Counted, not inferred from the row count — which would be wrong for exactly the
              caller who holds the limit exactly. */}
          <span className="ec-sub" data-testid="conversations-count">
            {rows.length === total
              ? t("dialog.conversations.countAll", { count: total })
              : t("dialog.conversations.countSome", { shown: rows.length, count: total })}
          </span>
        </div>
      </div>
      <div className="d-body flex flex-col gap-1" data-testid="conversations-rows">
        {rows.map((conversation) => (
          <Row key={conversation.sessionId} conversation={conversation} onDelete={onDelete} />
        ))}
      </div>
    </section>
  );
}

function Row({
  conversation,
  onDelete,
}: {
  conversation: SpecDialogSessionSummary;
  onDelete: (conversation: SpecDialogSessionSummary) => void;
}) {
  const t = useT();
  const filed = outcomeLabel(t, conversation);
  // 2026-09-21-f237a: the row says what the conversation is ABOUT; the delete says what the
  // person wrote. Opposite preferences over the same two strings, the same way the panel's row
  // resolves them.
  const untitled = t("dialog.conversations.untitled", { id: conversation.sessionId });
  const name = conversation.subject ?? conversation.title ?? untitled;
  const written = conversation.title ?? conversation.subject ?? untitled;
  return (
    <div className="d-conv-row">
      <Link
        href={conversationHref(conversation)}
        data-testid={`conversations-open-${conversation.sessionId}`}
        className="d-conv"
      >
        <span className="block dsh-body font-medium text-ink">{name}</span>
        <span className="ec-marks ec-sub items-center">
          <span className="ec-mark given">{projectLabel(conversation.project)}</span>
          {/* 2026-09-27-5c1eb: the same mark as the panel's row, built separately — see there. */}
          {conversation.ticket && (
            <span
              data-testid={`conversations-ticket-${conversation.sessionId}`}
              className="ec-mark filed"
            >
              {conversation.ticket}
            </span>
          )}
          <span>{t("dialog.conversations.turns", { count: conversation.turns })}</span>
          <span>{lastActive(t, conversation.lastActivityAt)}</span>
          {conversation.openDialogId && <span data-testid="conversations-open-mark">{t("dialog.conversations.openMark")}</span>}
          {filed && <span className="ec-mark filed">{filed}</span>}
        </span>
      </Link>
      <button
        type="button"
        data-testid={`conversations-delete-${conversation.sessionId}`}
        aria-label={t("dialog.conversations.deleteNamed", { name: written })}
        title={t("dialog.conversations.deleteThis")}
        onClick={() => onDelete(conversation)}
        className="d-conv-x"
      >
        ×
      </button>
    </div>
  );
}
