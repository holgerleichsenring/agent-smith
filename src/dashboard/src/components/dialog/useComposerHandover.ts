"use client";

import { useEffect, useRef } from "react";
import type { SpecDialogState } from "@/hooks/useSpecDialog";
import { takeHandover, type WorkItOutHandover } from "@/lib/workItOutHandover";

// 2026-11-17-d6fba: a conversation started on the home page arrives here as a held first message. It is taken once on
// mount — its project picked at once, so the page shows the conversation's project rather than the choice — and sent
// once the dialog view has been read, through the hook's own attach, attachSite and send: the files first, because a
// message sent ahead of its files starts a turn that never reads them.

export function useComposerHandover(dialog: SpecDialogState, pick: (project: string) => void): void {
  const pending = useRef<WorkItOutHandover | null>(null);
  const picked = useRef(pick);
  picked.current = pick;
  useEffect(() => {
    pending.current ??= takeHandover();
    if (pending.current) picked.current(pending.current.project);
  }, []);
  const { dialogId, view, attach, attachSite, send } = dialog;
  useEffect(() => {
    const handover = pending.current;
    if (!handover || !dialogId || !view) return;
    pending.current = null;
    void (async () => {
      for (const image of handover.images) await attach(image, handover.project);
      for (const set of handover.sets) await attachSite(set.files, handover.project, set.leftOut);
      await send(handover.text, handover.project);
    })();
  }, [dialogId, view, attach, attachSite, send]);
}
