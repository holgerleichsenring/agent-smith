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

/** A fresh conversation: a new id, held from now on. */
export function startNewDialog(): string {
  return hold(mintDialogId());
}

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
