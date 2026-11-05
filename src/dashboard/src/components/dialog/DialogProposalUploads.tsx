"use client";

import type { SpecDialogUploadRead } from "@/types/spec-dialog";

// 2026-11-04-96a0c: how much of each upload the proposal stands on — the readable files the proposing turn opened
// with read_file, and how many of them the conversation has opened. A turn's own account of what it read is a guess;
// these counts are what the server saw. A proposal stored before this shipped carries no uploads and shows nothing.

export function DialogProposalUploads({ uploads }: { uploads?: SpecDialogUploadRead[] }) {
  if (!uploads?.length) return null;
  return (
    <ul data-testid="dialog-proposal-uploads" className="mt-1.5 mb-1.5 flex flex-col gap-0.5">
      {uploads.map((upload) => (
        <li key={upload.setId} data-testid="dialog-proposal-upload" className="dsh-body text-ink">
          <span className="dsh-mono font-mono">{upload.address}</span>
          {` — this turn opened ${upload.thisTurn}; the conversation ${upload.opened} of ${upload.files}`}
        </li>
      ))}
    </ul>
  );
}
