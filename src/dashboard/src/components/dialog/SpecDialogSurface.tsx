"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";

import type { SpecDialogProposalPush } from "@/types/spec-dialog";
import { useFiledWork } from "@/hooks/useFiledWork";
import { useSpecDialog } from "@/hooks/useSpecDialog";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { PageHead } from "@/components/system/PageHead";
import { ConfirmDialog, useConfirmDialog } from "./ConfirmDialog";
import { deletionWarning } from "./conversationDelete";
import { DialogComposer } from "./DialogComposer";
import { DialogConversations } from "./DialogConversations";
import { DialogPane, useDialogPaneFocus } from "./DialogPane";
import { DialogQuestionCard } from "./DialogQuestionCard";
import { DialogTranscript } from "./DialogTranscript";
import { DialogWorking } from "./DialogWorking";

// 2026-09-15-cb3e: the design conversation, in the dashboard.
// 2026-09-17-c7aed: Work it out, in three columns — the caller's conversations, the exchange,
// and the pane with what the conversation reads, proposes and filed. The surface is a size
// container and collapses by the width the shell leaves it, not by the window's: the
// conversations column goes above first, then the pane stacks under the exchange.
// 2026-09-17-042ef: and it runs under a shell of its OWN — .mock-dialog — rather than under
// the runs list's, whose `section + section` margin dropped the middle column 26px below its
// neighbours, and rather than under the config studio's, whose .main caps at 1000px, below
// the width the third column appears at. The columns themselves stay container-queried: the
// surface collapses by the width the shell leaves it, not by the window's.

export const WORK_IT_OUT = "Work it out";

/** The address the conversations page links to: which conversation to open, and — when the row
 *  said it was open — the dialog id it is already living on. */
export const OPEN_PARAM = "open";
export const ON_PARAM = "on";

/**
 * 2026-09-21-f237b: a conversation handed to this surface by its address.
 *
 * Opening one is a hook callback and never was an address: a CLOSED conversation is resumed by a
 * command queued until this page has joined the new dialog id's hub group, which no link can do.
 * So the page links here and the hook still does the opening — with the dialog id the row carried
 * where there was one, because that is the only thing that tells `open` to GO to a running
 * conversation rather than resume it, and a resume is refused while a turn runs.
 *
 * Consumed once and then struck from the address, so a reload does not reopen what the operator
 * has since navigated away from, and the browser's own history stops carrying it.
 */
function useHandover(open: (sessionId: string, openDialogId?: string | null) => void) {
  const params = useSearchParams();
  const router = useRouter();
  const taken = useRef<string | null>(null);
  const sessionId = params.get(OPEN_PARAM);
  const onDialogId = params.get(ON_PARAM);
  useEffect(() => {
    if (!sessionId || taken.current === sessionId) return;
    taken.current = sessionId;
    open(sessionId, onDialogId);
    router.replace("/spec-dialog");
  }, [sessionId, onDialogId, open, router]);
}

/** How long the pane wears the mark an inspect puts on it. Long enough to be read as an answer
 *  to the click, short enough that it is gone before the operator reads what it selected. */
const MARK_MS = 1400;

export function SpecDialogSurface() {
  const dialog = useSpecDialog();
  useHandover(dialog.open);
  // The picked project lives here rather than in the list, because SENDING needs it too: a
  // message typed with no session open has to open one, and the project is what opens it.
  // With a single configured project there is no picker and no choice to make.
  const projects = dialog.view?.projects ?? [];
  const [picked, setPicked] = useState("");
  const project = picked || (projects.length === 1 ? projects[0].name : "");
  const session = dialog.view?.session ?? null;
  const mustPick = !session && project === "";
  const [focus, setFocus] = useDialogPaneFocus(dialog.proposal, dialog.filed);
  // 2026-09-20-4b0ae: INSPECT ACKNOWLEDGES ITSELF. The pane selects the proposal tab by
  // fallback whenever nothing has been filed, so the commonest state is the pane already
  // showing the very proposal the card is offering to inspect — setting the focus to it then
  // renders identically and the control looks dead. The act fires the acknowledgement, not a
  // change derived from it: a count of inspects, taken off again on a timer, and each click
  // restarts that timer because the count it depends on moved.
  const pane = useRef<HTMLElement>(null);
  const panel = useRef<HTMLDivElement>(null);
  const [inspects, setInspects] = useState(0);
  useEffect(() => {
    if (inspects === 0) return;
    const timer = window.setTimeout(() => setInspects(0), MARK_MS);
    return () => window.clearTimeout(timer);
  }, [inspects]);
  const inspect = (proposal: SpecDialogProposalPush) => {
    setFocus({ tab: "proposal", proposal });
    setInspects((seen) => seen + 1);
    // Nearest, and no behaviour: the page's scroll container is the main region and the pane's
    // top is the top of the grid, so a start-aligned scroll would throw the page back to the top
    // of a long transcript the operator was reading at the bottom of. A pane already in view is
    // not moved at all.
    pane.current?.scrollIntoView({ block: "nearest", inline: "nearest" });
    panel.current?.focus();
  };
  // 2026-09-17-042ej: the conversation follows what it filed. A read of its own rather than a
  // field on the dialog view, which is refetched after every reply.
  const work = useFiledWork(dialog.dialogId, dialog.filed);
  // 2026-09-20-4b0ab: asking is the SURFACE's job, because a hook cannot render. The rule that
  // decides whether a conversation needs confirming, and what the warning says, stays where it
  // was; what changes is that the warning is now put to the reader in a dialog this page drew.
  const confirmation = useConfirmDialog();
  async function remove(sessionId: string) {
    // The list renders a delete only for a row it holds, so the lookup finds one; a caller
    // that reached this with an id the list does not hold deletes without confirming, which is
    // what the hook did before the asking moved here.
    const listed = dialog.conversations.find((held) => held.sessionId === sessionId);
    const warning = listed ? deletionWarning(listed) : null;
    if (warning !== null && !(await confirmation.ask(warning, { confirmLabel: "Delete" }))) return;
    await dialog.remove(sessionId);
  }
  // 2026-09-20-4b0af: the heading says what the conversation is ABOUT, and falls back to the
  // first line the person wrote — which is what it always said. The subject rides the SESSION,
  // re-read after every reply, so the heading corrects itself on the next read; the list keeps
  // being read only while its own predicate says so.
  // 2026-09-21-f237a: the row beside it now prefers the subject too, so the two agree wherever
  // one has been minted. They are still read from different places — the heading from the
  // session, the row from the list — because only the session read is issued after every reply.
  const listed = session
    ? dialog.conversations.find((held) => held.sessionId === session.sessionId)?.title ?? null
    : null;
  const title = session?.subject ?? listed;

  return (
    <div className="mock-shell mock-dialog" data-testid="spec-dialog">
      <main className="main">
        <PageHead
          title={WORK_IT_OUT}
          sub="Say what you want to be true. Leave with tickets someone can run."
        />
        {/* A refusal reads as one here: FailedSurface branches on it, so a caller who
            may not hold spec dialogs is told that rather than shown a stack. */}
        {dialog.failure && <FailedSurface surface={`${WORK_IT_OUT} page`} error={dialog.failure} />}
        <div className="@container">
          <div className="grid grid-cols-1 items-start gap-4 @3xl:grid-cols-[minmax(0,1fr)_300px] @6xl:grid-cols-[220px_minmax(0,1fr)_360px]">
            <DialogConversations
              dialogId={dialog.dialogId}
              sessionHere={session?.sessionId ?? null}
              projects={projects}
              conversations={dialog.conversations}
              picked={picked}
              onPicked={setPicked}
              onStartNew={(chosen) => void dialog.startNew(chosen)}
              onOpen={(sessionId, openDialogId) => void dialog.open(sessionId, openDialogId)}
              onDelete={(sessionId) => void remove(sessionId)}
            />
            <section className="ecard inert min-w-0">
              <div className="d-head">
                {/* 2026-09-17-042em: the header names the open session's PROJECT. The picker
                    beside the list stays a choice — it feeds New conversation, and disabling it
                    on the open session's project would stop a new conversation on another one —
                    so this is where a person reads what the conversation they are in is about. */}
                <div className="d-head-t">
                  <h2 data-testid="dialog-heading" className="ec-name sans min-w-0">
                    {title ?? "New conversation"}
                  </h2>
                  {session && (
                    <span data-testid="dialog-exchange-project" className="ec-sub">
                      in <span className="fv">{session.scope.name}</span>
                    </span>
                  )}
                </div>
                {/* The dialog id IS the session key: a page that lost it would open a second
                    conversation beside one still running, and an operator comparing two tabs
                    needs to see which is which. */}
                <span data-testid="dialog-identity" className="ec-sub font-mono">
                  {session
                    ? `session ${session.sessionId} · dialog ${dialog.dialogId ?? ""}`
                    : `dialog ${dialog.dialogId ?? ""} · no session open`}
                </span>
              </div>
              <div className="d-body flex flex-col gap-4">
                <DialogTranscript
                  entries={dialog.entries}
                  onInspect={inspect}
                />
                {/* 2026-09-18-2f8b: this page's own post OR a turn the view says is running,
                    so a page arriving mid-turn is not shown a conversation that looks over. */}
                {dialog.working && (
                  <DialogWorking
                    readings={dialog.readings}
                    activity={dialog.activity}
                    since={dialog.workingSince}
                  />
                )}
                {dialog.question && (
                  <DialogQuestionCard
                    question={dialog.question}
                    proposal={dialog.proposal}
                    onAnswer={(answer, decision) => void dialog.send(answer, project, decision)}
                  />
                )}
              </div>
              <DialogComposer
                disabled={!dialog.dialogId || mustPick}
                hint={mustPick ? "Pick a project first — that is what a conversation reads." : undefined}
                onSend={(text) => void dialog.send(text, project)}
                onAttach={(file) => void dialog.attach(file, project)}
              />
            </section>
            <DialogPane
              session={session}
              projects={projects}
              proposal={dialog.proposal}
              filed={dialog.filed}
              work={work}
              focus={focus}
              onFocus={setFocus}
              paneRef={pane}
              panelRef={panel}
              marked={inspects > 0}
            />
          </div>
        </div>
      </main>
      {/* Inside this page's shell, because that is what the confirmation's rules are scoped
          to; showModal lifts it to the top layer without moving it in the DOM. */}
      <ConfirmDialog {...confirmation.dialog} />
    </div>
  );
}
