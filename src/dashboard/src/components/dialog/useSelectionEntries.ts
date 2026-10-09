"use client";

import { useEffect, useState } from "react";
import type { SpecDialogHeldContent } from "@/types/spec-dialog";
import { heldIn, markHeld } from "./heldFiles";
import { entriesOf, gitignoresOf, type SelectionEntry } from "./referenceSelection";

// 2026-10-02-075db: the .gitignore files are read before anything is shown: an entry shown
// ticked that then unticks itself is a card that changed under the operator's hand.
// 2026-10-09-86e1: and so are the files the conversation already holds, for the same reason.

export interface SelectionRead {
  entries: SelectionEntry[];
  /** False when the page could not compare the pick with what is held (no crypto.subtle, or the read failed). */
  checked: boolean;
}

export function useSelectionEntries(
  files: File[],
  loadHeld?: () => Promise<SpecDialogHeldContent[]>,
): SelectionRead | null {
  const [read, setRead] = useState<SelectionRead | null>(null);
  useEffect(() => {
    let live = true;
    const held = (loadHeld ? loadHeld() : Promise.resolve([])).then((h) => heldIn(files, h)).catch(() => null);
    void Promise.all([gitignoresOf(files), held]).then(([gitignores, found]) => {
      if (!live) return;
      const entries = entriesOf(files, gitignores);
      setRead({ entries: found ? markHeld(entries, found) : entries, checked: found !== null });
    });
    return () => {
      live = false;
    };
    // The pick is what is read; loadHeld is a fresh closure on every render of the composer.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [files]);
  return read;
}
