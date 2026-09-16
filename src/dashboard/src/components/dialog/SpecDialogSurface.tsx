"use client";

import { useSpecDialog } from "@/hooks/useSpecDialog";
import { FailedSurface } from "@/components/shell/FailedSurface";
import { PageHead } from "@/components/system/PageHead";
import { DialogComposer } from "./DialogComposer";
import { DialogColumn } from "./DialogColumn";
import { DialogQuestionCard } from "./DialogQuestionCard";
import { DialogSessionControls } from "./DialogSessionControls";
import { DialogTranscript } from "./DialogTranscript";

// 2026-09-15-cb3e: the design conversation, in the dashboard. Two columns: the conversation
// on the left, and on the right ONE column that changes with the conversation —
// 2026-09-15-6d9c: what the agent may read, then the proposal under discussion, then the
// tickets it was filed as.

export function SpecDialogSurface() {
  const dialog = useSpecDialog();

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
          onStartNew={(project) => void dialog.startNew(project)}
          onResume={(sessionId) => void dialog.resume(sessionId)}
        />
        {/* A refusal reads as one here: FailedSurface branches on it, so a caller who
            may not hold spec dialogs is told that rather than shown a stack. */}
        {dialog.failure && <FailedSurface surface="the design dialog" error={dialog.failure} />}
        <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
          <section className="flex flex-col gap-3">
            <DialogTranscript entries={dialog.entries} />
            {dialog.question && (
              <DialogQuestionCard
                question={dialog.question}
                onAnswer={(answer) => void dialog.send(answer)}
              />
            )}
            <DialogComposer
              disabled={!dialog.dialogId}
              onSend={(text) => void dialog.send(text)}
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
