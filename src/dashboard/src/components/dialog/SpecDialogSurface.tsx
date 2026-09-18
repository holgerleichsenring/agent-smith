"use client";

import { useState } from "react";

import { useFiledWork } from "@/hooks/useFiledWork";
import { useSpecDialog } from "@/hooks/useSpecDialog";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { PageHead } from "@/components/system/PageHead";
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

export const WORK_IT_OUT = "Work it out";

export function SpecDialogSurface() {
  const dialog = useSpecDialog();
  // The picked project lives here rather than in the list, because SENDING needs it too: a
  // message typed with no session open has to open one, and the project is what opens it.
  // With a single configured project there is no picker and no choice to make.
  const projects = dialog.view?.projects ?? [];
  const [picked, setPicked] = useState("");
  const project = picked || (projects.length === 1 ? projects[0].name : "");
  const session = dialog.view?.session ?? null;
  const mustPick = !session && project === "";
  const [focus, setFocus] = useDialogPaneFocus(dialog.proposal, dialog.filed);
  // 2026-09-17-042ej: the conversation follows what it filed. A read of its own rather than a
  // field on the dialog view, which is refetched after every reply.
  const work = useFiledWork(dialog.dialogId, dialog.filed);
  const title = session
    ? dialog.conversations.find((held) => held.sessionId === session.sessionId)?.title ?? null
    : null;

  return (
    <div className="mock-shell mock-runs" data-testid="spec-dialog">
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
            />
            <section className="min-w-0 rounded-md border border-mute bg-canvas">
              <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1 border-b border-mute px-3 py-2">
                {/* 2026-09-17-042em: the header names the open session's PROJECT. The picker
                    beside the list stays a choice — it feeds New conversation, and disabling it
                    on the open session's project would stop a new conversation on another one —
                    so this is where a person reads what the conversation they are in is about. */}
                <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-0.5">
                  <h2 className="min-w-0 dsh-body font-semibold text-ink">{title ?? "New conversation"}</h2>
                  {session && (
                    <span data-testid="dialog-exchange-project" className="dsh-label text-body">
                      in <span className="font-mono text-ink">{session.scope.name}</span>
                    </span>
                  )}
                </div>
                {/* The dialog id IS the session key: a page that lost it would open a second
                    conversation beside one still running, and an operator comparing two tabs
                    needs to see which is which. */}
                <span data-testid="dialog-identity" className="font-mono dsh-label text-body">
                  {session
                    ? `session ${session.sessionId} · dialog ${dialog.dialogId ?? ""}`
                    : `dialog ${dialog.dialogId ?? ""} · no session open`}
                </span>
              </div>
              <div className="flex flex-col gap-4 px-4 py-4">
                <DialogTranscript
                  entries={dialog.entries}
                  onInspect={(proposal) => setFocus({ tab: "proposal", proposal })}
                />
                {dialog.awaiting && <DialogWorking readings={dialog.readings} activity={dialog.activity} />}
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
            />
          </div>
        </div>
      </main>
    </div>
  );
}
