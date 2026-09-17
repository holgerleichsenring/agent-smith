"use client";

import { useState } from "react";

import { useSpecDialog } from "@/hooks/useSpecDialog";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { PageHead } from "@/components/system/PageHead";
import { DialogComposer } from "./DialogComposer";
import { DialogColumn } from "./DialogColumn";
import { DialogQuestionCard } from "./DialogQuestionCard";
import { DialogSessionControls } from "./DialogSessionControls";
import { DialogTranscript } from "./DialogTranscript";
import type { SpecDialogReadingState } from "@/types/spec-dialog";

// 2026-09-15-cb3e: the design conversation, in the dashboard. Two columns: the conversation
// on the left, and on the right ONE column that changes with the conversation —
// 2026-09-15-6d9c: what the agent may read, then the proposal under discussion, then the
// tickets it was filed as.

const READING_WORDS: Record<SpecDialogReadingState, string> = {
  opening: "opening",
  ready: "ready",
  failed: "could not be opened",
};

export function SpecDialogSurface() {
  const dialog = useSpecDialog();
  // The picked project lives here rather than in the controls, because SENDING needs it
  // too: a message typed with no session open has to open one, and the project is what
  // opens it. With a single configured project there is no picker and no choice to make.
  const projects = dialog.view?.projects ?? [];
  const [picked, setPicked] = useState("");
  const project = picked || (projects.length === 1 ? projects[0].name : "");
  const mustPick = !dialog.view?.session && project === "";

  return (
    <div className="mock-shell mock-runs" data-testid="spec-dialog">
      <main className="main">
        <PageHead
          title="Design dialog"
          sub="Design what to build with the agent, and approve what gets filed."
        />
        <DialogSessionControls
          dialogId={dialog.dialogId}
          view={dialog.view}
          conversations={dialog.conversations}
          picked={picked}
          onPicked={setPicked}
          onStartNew={(project) => void dialog.startNew(project)}
          onOpen={(sessionId, openDialogId) => void dialog.open(sessionId, openDialogId)}
        />
        {/* A refusal reads as one here: FailedSurface branches on it, so a caller who
            may not hold spec dialogs is told that rather than shown a stack. */}
        {dialog.failure && <FailedSurface surface="the design dialog" error={dialog.failure} />}
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
          <section className="flex flex-col gap-3">
            <DialogTranscript entries={dialog.entries} />
            {dialog.awaiting && (
              <div
                data-testid="dialog-working"
                className="flex flex-col gap-1 dsh-body text-[var(--color-ink-mid)]"
              >
                <span className="flex items-center gap-2">
                  <span
                    aria-hidden="true"
                    className="inline-block size-3 animate-spin rounded-full border-2 border-current border-t-transparent"
                  />
                  Reading the repositories and thinking — this takes a minute.
                </span>
                {dialog.readings.length > 0 && (
                  <ul data-testid="dialog-readings">
                    {dialog.readings.map((reading) => (
                      <li key={reading.repo} data-testid="dialog-reading" data-state={reading.state}>
                        {reading.repo}: {READING_WORDS[reading.state]}
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            )}
            {dialog.question && (
              <DialogQuestionCard
                question={dialog.question}
                onAnswer={(answer) => void dialog.send(answer, project)}
              />
            )}
            <DialogComposer
              disabled={!dialog.dialogId || mustPick}
              hint={mustPick ? "Pick a project first — that is what a conversation reads." : undefined}
              onSend={(text) => void dialog.send(text, project)}
            />
          </section>
          <DialogColumn
            session={dialog.view?.session ?? null}
            projects={dialog.view?.projects ?? []}
            proposal={dialog.proposal}
            filed={dialog.filed}
          />
        </div>
      </main>
    </div>
  );
}
