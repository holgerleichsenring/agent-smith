"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { SpecDialogState } from "@/hooks/useSpecDialog";
import { takeHandover, type WorkItOutHandover } from "@/lib/workItOutHandover";

// 2026-11-17-d6fba: a conversation started on the home page arrives here as a held first message. It is taken once on
// mount — its project picked at once, so the page shows the conversation's project rather than the choice — and sent
// once the dialog view has been read, through the hook's own attach, attachSite and send: the files first, because a
// message sent ahead of its files starts a turn that never reads them.
// 2026-11-17-76f1: and a new conversation on this page starts the same way — its composer is the home page's, so what it
// collects is a hand-over too, given to `start`. `started` holds from a hand-over until the page moves to another dialog
// id (a new conversation, or one opened), so the page leaves its new-work state the moment one is under way.

export interface ComposerHandover {
  start: (handover: WorkItOutHandover) => void;
  started: boolean;
}

export function useComposerHandover(dialog: SpecDialogState, pick: (project: string) => void): ComposerHandover {
  const pending = useRef<WorkItOutHandover | null>(null);
  const picked = useRef(pick);
  picked.current = pick;
  const [started, setStarted] = useState(false);
  const [asked, setAsked] = useState(0);
  // The dialog id the hand-over was sent on; null until it is sent, which may wait for the id to be read.
  const sentOn = useRef<string | null>(null);
  useEffect(() => {
    pending.current ??= takeHandover();
    if (pending.current) {
      picked.current(pending.current.project);
      setStarted(true);
    }
  }, []);
  const { dialogId, view, attach, attachSite, send } = dialog;
  useEffect(() => {
    const handover = pending.current;
    if (!handover || !dialogId || !view) return;
    pending.current = null;
    sentOn.current = dialogId;
    void (async () => {
      for (const image of handover.images) await attach(image, handover.project);
      for (const set of handover.sets) await attachSite(set.files, handover.project, set.leftOut);
      await send(handover.text, handover.project);
    })();
  }, [dialogId, view, attach, attachSite, send, asked]);
  const start = useCallback((handover: WorkItOutHandover) => {
    pending.current = handover;
    sentOn.current = null;
    picked.current(handover.project);
    setStarted(true);
    setAsked((n) => n + 1);
  }, []);
  return { start, started: started && (sentOn.current === null || sentOn.current === dialogId) };
}
