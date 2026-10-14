"use client";

import { useState } from "react";
import { mockPath } from "@/lib/referenceFilesApi";
import { ratifySpecMock } from "@/lib/specDialogApi";
import type { SpecDialogMock } from "@/types/spec-dialog";
import { AuthedImage } from "./AuthedImage";

// 2026-10-13-8762t: one spec's mock versions on the proposal, v1..vN as their desktop screenshots — never
// the page, which would run as the dashboard on this origin. The ratified one is outlined; every other one
// offers Ratify until the proposal is filed, after which a new ratification reaches the spec only through a
// new approval, so the strip is read-only and says so.

interface Props {
  specId: string;
  /** This spec's versions, oldest first. */
  mocks: SpecDialogMock[];
  ratified: number | null;
  sessionId: string;
  onRatified?: () => Promise<void>;
  filed: boolean;
}

export function MockVersions({ specId, mocks, ratified, sessionId, onRatified, filed }: Props) {
  const [busy, setBusy] = useState(false);
  const [refusal, setRefusal] = useState<string | null>(null);
  if (mocks.length === 0) return null;

  const ratify = async (version: number) => {
    setBusy(true);
    setRefusal(null);
    try {
      await ratifySpecMock(sessionId, specId, version);
      await onRatified?.();
    } catch (err) {
      setRefusal(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="mt-2" data-testid={`dialog-mock-versions-${specId}`}>
      <div className="fl mb-0.5">Mock versions</div>
      <ol className="flex gap-2 overflow-x-auto pb-1">
        {mocks.map((mock) => {
          const isRatified = mock.version === ratified;
          const id = `${specId}-v${mock.version}`;
          return (
            <li
              key={mock.version}
              data-testid={`dialog-mock-version-${id}`}
              data-ratified={isRatified ? "true" : undefined}
              className={`ecard inert flex w-40 shrink-0 flex-col gap-1 p-1.5 ${isRatified ? "border-2 border-state-proven" : ""}`}
            >
              <div className="flex items-baseline justify-between gap-1">
                <span className="font-mono dsh-mono text-ink">{`v${mock.version}`}</span>
                {isRatified && <span className="dsh-mono font-semibold text-state-proven">ratified</span>}
              </div>
              {mock.hasShot ? (
                <AuthedImage
                  testId={`dialog-mock-version-shot-${id}`}
                  path={mockPath(sessionId, specId, mock.version, "shot")}
                  alt={`Mock v${mock.version} of ${specId}, desktop`}
                  className="max-h-28 w-full object-cover object-top"
                />
              ) : (
                <span className="ec-sub" data-testid={`dialog-mock-version-noshot-${id}`}>{`v${mock.version} — no screenshot`}</span>
              )}
              {!isRatified && !filed && (
                <button type="button" className="btn" disabled={busy} data-testid={`dialog-mock-ratify-${id}`}
                  onClick={() => void ratify(mock.version)}>
                  Ratify
                </button>
              )}
            </li>
          );
        })}
      </ol>
      {filed && (
        <p className="ec-sub" data-testid={`dialog-mock-filed-${specId}`}>
          {`${ratified ? `filed with v${ratified}` : "filed"}; a new ratification reaches the spec only through a new approval of this ticket`}
        </p>
      )}
      {refusal && <p className="dsh-body text-state-stopped" role="alert" data-testid={`dialog-mock-refused-${specId}`}>{refusal}</p>}
    </div>
  );
}
