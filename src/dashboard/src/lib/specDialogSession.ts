// 2026-09-15-cb3e: the dialog id lives in the BROWSER, which is what 2026-09-15-9033
// decided: a session is keyed on (platform, threadId) and the thread id is this. An id
// lost on reload would silently start a second conversation beside the one still open, so
// it is held — and the page says which session it is in.
//
// Storage can refuse (a private window, site data blocked) and a refusal must not cost the
// page its conversation, so the id is also held in memory for the life of the document.
// A reload there starts a new conversation, which is the honest outcome of a browser that
// remembers nothing.

const STORAGE_KEY = "agentsmith.spec-dialog.dialog-id";

let inMemory: string | null = null;

/** The dialog id this browser is holding, minting one on first use. */
export function currentDialogId(): string {
  return held() ?? hold(mintDialogId());
}

/** Back to a conversation that is open on an id this page already knows, held from now on. */
export function returnToDialog(dialogId: string): string {
  return hold(dialogId);
}

/** A fresh conversation: a new id, held from now on. */
export function startNewDialog(): string {
  return hold(mintDialogId());
}

/**
 * 2026-10-13-9a59r: where a conversation surface takes its dialog id from. Work it out HOLDS the
 * browser's one id; a surface opened on a ticket mints its own per instance and holds nothing, so
 * opening a ticket never rewrites the operator's Work it out conversation.
 */
export interface DialogIdSource {
  /** The id a freshly mounted surface starts on. */
  seed(): string;
  /** The id a switch moves to — `to` when going to a dialog already known, else a fresh one. */
  switchTo(to?: string): string;
}

/** The browser's one held id — Work it out. */
export const HELD_DIALOG_ID: DialogIdSource = {
  seed: () => currentDialogId(),
  switchTo: (to) => (to ? returnToDialog(to) : startNewDialog()),
};

/** An id per surface instance, never held: what was held before stays held. */
export const INSTANCE_DIALOG_ID: DialogIdSource = {
  seed: () => mintDialogId(),
  switchTo: (to) => to ?? mintDialogId(),
};

export function mintDialogId(): string {
  const random = globalThis.crypto?.randomUUID?.();
  return `d-${(random ?? `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 10)}`)
    .replace(/-/g, "")
    .slice(0, 16)}`;
}

function held(): string | null {
  if (inMemory) return inMemory;
  try {
    inMemory = window.localStorage.getItem(STORAGE_KEY);
  } catch (cause) {
    console.debug("the spec dialog id could not be read from storage", cause);
  }
  return inMemory;
}

function hold(dialogId: string): string {
  inMemory = dialogId;
  try {
    window.localStorage.setItem(STORAGE_KEY, dialogId);
  } catch (cause) {
    console.debug("the spec dialog id could not be held across a reload", cause);
  }
  return dialogId;
}

/** Test-only: forget what this document is holding. */
export function __forgetDialogIdForTests(): void {
  inMemory = null;
}
