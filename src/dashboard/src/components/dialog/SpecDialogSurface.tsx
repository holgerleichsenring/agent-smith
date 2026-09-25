"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";

import type { SpecDialogProposalPush } from "@/types/spec-dialog";
import type { TicketProjectRead } from "@/lib/specDialogApi";
import { readTicketConversation, readTicketProject } from "@/lib/specDialogApi";
import { useFiledWork } from "@/hooks/useFiledWork";
import { useSpecDialog } from "@/hooks/useSpecDialog";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { PageHead } from "@/components/system/PageHead";
import { ConfirmDialog, useConfirmDialog } from "./ConfirmDialog";
import { deletionWarning } from "./conversationDelete";
import { DialogComposer } from "./DialogComposer";
import { DialogConversations } from "./DialogConversations";
import { DialogPane, useDialogPaneFocus } from "./DialogPane";
import { DialogProjectChoice, type ProjectsRead } from "./DialogProjectChoice";
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

/** 2026-09-25-8e51b: the ticket a page was opened on, and the project it belongs to. */
export const TICKET_PARAM = "ticket";
export const PROJECT_PARAM = "project";

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

/**
 * 2026-09-25-8e51b: a page addressed with a TICKET. A ticket has one conversation, so the page
 * asks the server which one — and goes there when it is open, resumes it when it is closed, and
 * holds the ticket for its first message when there is none yet.
 *
 * The binding must ride on that first message: the message is what OPENS the conversation, and a
 * conversation that missed its binding can never be given one. It is consumed once and struck
 * from the address, like the conversation handover beside it.
 */
function useTicketHandover(open: (sessionId: string, openDialogId?: string | null) => void) {
  const params = useSearchParams();
  const router = useRouter();
  const taken = useRef<string | null>(null);
  const [pending, setPending] = useState<{ project: string; ticketId: string } | null>(null);
  const ticketId = params.get(TICKET_PARAM);
  const project = params.get(PROJECT_PARAM);
  // 2026-09-25-8e51a: an address that names only a ticket lets the TICKET name the project — its
  // own routing, on its own tracker. Several or none, and the choice stays with the operator.
  const [reason, setReason] = useState<string | null>(null);
  // The read is IN FLIGHT while the page loads its own fresh conversation, and `open` is a
  // callback over the view — so it is a new function by the time the answer lands. Depending on
  // it would tear this effect down mid-flight and the answer would be dropped, once, silently:
  // the address is consumed and the conversation the ticket has is never reached. The latest one
  // is held in a ref and the effect depends on the ADDRESS alone.
  const latest = useRef(open);
  latest.current = open;
  useEffect(() => {
    // The project is optional: without one the TICKET names it (8e51a).
    if (!ticketId || taken.current === ticketId) return;
    taken.current = ticketId;
    void Promise.resolve(
      project ? readTicketConversation(project, ticketId) : readTicketProject(ticketId),
    )
      .then((held) => {
        if (held?.sessionId) {
          latest.current(held.sessionId, held.openDialogId);
          return;
        }
        const named = project ? [project] : ((held as TicketProjectRead | null)?.projects ?? []);
        if (named.length === 1) setPending({ project: named[0], ticketId });
        else setReason(whyNoProject(named, (held as TicketProjectRead | null)?.unanswerable ?? []));
      })
      // A ticket the tracker does not have, or a read that failed: the page stays usable and the
      // operator is not handed a conversation bound to something that is not there.
      .catch(() => {})
      .finally(() => router.replace("/spec-dialog"));
  }, [ticketId, project, router]);
  return { pending, reason };
}

/** Why the ticket did not name one project — a reason, never an accusation. */
function whyNoProject(named: string[], unanswerable: string[]): string {
  if (named.length > 1)
    return `This ticket's labels name ${named.length} projects (${named.join(", ")}). Choose one.`;
  if (unanswerable.length > 0)
    return (
      `No project matched this ticket's labels. ${unanswerable.join(", ")} ` +
      "routes by area path, repository or address, which a ticket read by its id cannot carry — " +
      "so it could not be answered for rather than ruled out. Choose a project."
    );
  return "No project's routing names this ticket. Choose one.";
}

/** How long the pane wears the mark an inspect puts on it. Long enough to be read as an answer
 *  to the click, short enough that it is gone before the operator reads what it selected. */
const MARK_MS = 1400;

export function SpecDialogSurface() {
  const dialog = useSpecDialog();
  useHandover(dialog.open);
  const { pending: pendingTicket, reason: ticketReason } = useTicketHandover(dialog.open);
  // The picked project lives here rather than in the list, because SENDING needs it too: a
  // message typed with no session open has to open one, and the project is what opens it.
  // With a single configured project there is no picker and no choice to make.
  const projects = dialog.view?.projects ?? [];
  const [picked, setPicked] = useState("");
  // 2026-09-25-8e51b: a page opened on a ticket already knows its project — it had to, to ask
  // which conversation that ticket has — so there is nothing left to pick.
  const project =
    picked || pendingTicket?.project || (projects.length === 1 ? projects[0].name : "");
  const session = dialog.view?.session ?? null;
  const mustPick = !session && project === "";
  // 2026-09-23-6e3f: what the choice may say while it holds no projects. The list is empty in
  // all three states, so the VIEW is what tells them apart: a read that has not answered leaves
  // it null, and a read that failed leaves the same null for ever with a failure beside it.
  const read: ProjectsRead = dialog.view ? "read" : dialog.failure ? "failed" : "pending";
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
          {/* 2026-09-21-f237c: the panel's track goes 220px -> 300px, and the EXCHANGE pays. The
              scope pane renders given names — repositories, templates, revisions — that this page
              did not choose and cannot shorten without lying, so it keeps its width; the exchange
              is prose and reflows. The mid and phone breakpoints, where the panel is already full
              width, are unchanged. */}
          <div className="grid grid-cols-1 items-start gap-4 @3xl:grid-cols-[minmax(0,1fr)_300px] @6xl:grid-cols-[300px_minmax(0,1fr)_360px]">
            <DialogConversations
              dialogId={dialog.dialogId}
              sessionHere={session?.sessionId ?? null}
              conversations={dialog.conversations}
              onStartNew={() => {
                // 2026-09-23-6e3f: the pick is cleared HERE, where it lives. The hook mints the
                // dialog id and clears the view, but it holds no reference to the pick — and a
                // new conversation that kept the last one's project would open on it without
                // ever having asked.
                setPicked("");
                void dialog.startNew();
              }}
              onOpen={(sessionId, openDialogId) => void dialog.open(sessionId, openDialogId)}
              onDelete={(sessionId) => void remove(sessionId)}
            />
            <section className="ecard inert min-w-0">
              <div className="d-head">
                {/* 2026-09-17-042em: the header names the open session's PROJECT — this is
                    where a person reads what the conversation they are in is about.
                    2026-09-23-6e3f: and the choice that picks one for the NEXT conversation is
                    this column's empty state, so the two are never on screen together and
                    neither can be read as the other. */}
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
                {mustPick ? (
                  <DialogProjectChoice
                    projects={projects}
                    read={read}
                    picked={picked}
                    onPicked={setPicked}
                    reason={ticketReason}
                  />
                ) : (
                  <>
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
                  </>
                )}
              </div>
              {/* The first render holds no dialog id yet — it is read off the browser after the
                  mount — so the composer is still guarded on that. Nothing else disables it:
                  with no project picked there is no composer to disable. */}
              {!mustPick && (
                <DialogComposer
                  disabled={!dialog.dialogId}
                  onSend={(text) =>
                    // 2026-09-25-8e51b: the first message on a ticket-addressed page carries the
                    // binding, because that message is what opens the conversation.
                    void dialog.send(text, project, undefined, pendingTicket?.ticketId)
                  }
                  onAttach={(file) => void dialog.attach(file, project)}
                />
              )}
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
