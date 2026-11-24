"use client";

import { useT } from "@/hooks/useT";
import type { PocScanFinding } from "@/lib/pocsApi";
import { findingKindLabel, findingPlace } from "./BlockingFinding";

// 2026-11-23-933dc: the findings that do not block, folded under one line that counts them.

export function ScanNotes({ notes, open }: { notes: PocScanFinding[]; open?: boolean }) {
  const t = useT();
  if (notes.length === 0) return null;
  return (
    <details className="poc-box poc-notes" open={open} data-testid="poc-revision-notes">
      <summary><h2>{t("pocs.revision.notes.title", { count: notes.length })}</h2></summary>
      <ul>
        {notes.map((note) => {
          const kind = findingKindLabel(note.kind);
          return (
            <li key={note.id} data-testid={`poc-note-${note.id}`}>
              <span className="poc-kind">{kind ? t(kind) : note.kind}</span>
              <span>
                <b>{note.title}</b> <span className="mono poc-place">{findingPlace(note)}</span>
                {note.detail && <span className="poc-meta">{note.detail}</span>}
                {note.fix && <span className="poc-meta">{t("pocs.revision.fix")} {note.fix}</span>}
              </span>
            </li>
          );
        })}
      </ul>
    </details>
  );
}
