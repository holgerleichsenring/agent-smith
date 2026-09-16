"use client";

import { useEffect, useState } from "react";
import type { SpecDialogView } from "@/types/spec-dialog";

// 2026-09-15-cb3e: the four chat commands as controls. "/spec" and "/spec new <project>"
// are a project picker and a button, "/spec list" is the list below, and "/spec resume
// <id>" is clicking one of its rows. The strings still travel through the ingestion
// endpoint — the router parses them — but nobody has to type them.
//
// The dialog id is shown because it IS the session key: a page that lost it would open a
// second conversation beside one still running, and an operator comparing two tabs needs
// to see which is which.

export function DialogSessionControls({
  dialogId,
  view,
  onStartNew,
  onResume,
}: {
  dialogId: string | null;
  view: SpecDialogView | null;
  onStartNew: (project?: string) => void;
  onResume: (sessionId: string) => void;
}) {
  const projects = view?.projects ?? [];
  const [picked, setPicked] = useState("");
  // Minting a dialog id and opening a session on it are two steps, and a second click
  // between them mints a second id: the first session opens on an id nothing is watching
  // while the page sits on the second with no session and no explanation. One click per
  // dialog id, released when the page is looking at one.
  const [starting, setStarting] = useState(false);
  useEffect(() => setStarting(false), [dialogId]);
  const resumable = (view?.openSessions ?? []).filter(
    (session) => session.sessionId !== view?.session?.sessionId,
  );

  return (
    <div data-testid="dialog-controls" className="mb-3 flex flex-wrap items-center gap-2">
      <span data-testid="dialog-identity" className="dsh-label text-[var(--color-ink-mid)]">
        {view?.session
          ? `session ${view.session.sessionId} · dialog ${dialogId ?? ""}`
          : `dialog ${dialogId ?? ""} · no session open`}
      </span>
      {projects.length > 1 && (
        <select
          data-testid="dialog-project-picker"
          value={picked}
          onChange={(event) => setPicked(event.target.value)}
          className="rounded border border-stone-300 px-2 py-1 text-sm"
        >
          <option value="">pick a project…</option>
          {projects.map((project) => (
            <option key={project.name} value={project.name}>
              {project.name}
            </option>
          ))}
        </select>
      )}
      <button
        type="button"
        data-testid="dialog-new"
        disabled={starting}
        onClick={() => {
          setStarting(true);
          onStartNew(picked || undefined);
        }}
        className="rounded border border-stone-300 px-3 py-1 text-sm text-stone-700 hover:bg-stone-100 disabled:opacity-50"
      >
        New conversation
      </button>
      {resumable.map((session) => (
        <button
          key={session.sessionId}
          type="button"
          data-testid={`dialog-resume-${session.sessionId}`}
          onClick={() => onResume(session.sessionId)}
          className="rounded border border-stone-300 px-3 py-1 text-sm text-stone-700 hover:bg-stone-100"
        >
          {`resume ${session.sessionId} · ${session.project} · ${session.turns} turn(s)`}
        </button>
      ))}
    </div>
  );
}
