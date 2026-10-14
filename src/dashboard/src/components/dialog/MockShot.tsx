"use client";

import type { TranscriptMock } from "@/hooks/useSpecDialog";
import { fetchMockHtmlBlob, mockPath } from "@/lib/referenceFilesApi";
import { AuthedImage } from "./AuthedImage";
import { DialogMessage } from "./DialogTranscript";

// 2026-10-13-8762r: a mock version the design partner wrote, shown where it was written — its desktop
// screenshot, or that it was not rendered, and the whole page to open. The page is never opened from
// this origin, where it would run as the dashboard: it is saved as a file, through the bearer token.

export function MockShot({ mock }: { mock: TranscriptMock }) {
  const id = `${mock.specId}-v${mock.version}`;
  return (
    <DialogMessage who="agent" testId="dialog-turn-mock">
      <div data-testid={`dialog-mock-${id}`} className="ecard inert">
        <div className="flex flex-wrap items-center gap-2.5 px-3 py-2">
          <span className="ec-mark">mock</span>
          <span className="ec-name sans min-w-0 flex-1 given">{`Mock v${mock.version} — ${mock.specId}`}</span>
          <button type="button" className="btn" data-testid={`dialog-mock-open-${id}`} onClick={() => void download(mock)}>
            Open HTML
          </button>
        </div>
        <div className="px-3 pb-2">
          {mock.hasShot ? (
            <AuthedImage
              testId={`dialog-mock-shot-${id}`}
              path={mockPath(mock.sessionId, mock.specId, mock.version, "shot")}
              alt={`Mock v${mock.version} of ${mock.specId}, desktop`}
              className="max-h-96 max-w-full"
            />
          ) : (
            <span className="ec-sub" data-testid={`dialog-mock-unrendered-${id}`}>not rendered</span>
          )}
        </div>
      </div>
    </DialogMessage>
  );
}

/** Saves the page under its derived name, as an upload's file is saved. */
async function download(mock: TranscriptMock) {
  const url = URL.createObjectURL(await fetchMockHtmlBlob(mock.sessionId, mock.specId, mock.version));
  const link = document.createElement("a");
  link.href = url;
  link.download = `${mock.name}.html`;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
