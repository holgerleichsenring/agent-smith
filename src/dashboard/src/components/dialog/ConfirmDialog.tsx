"use client";

import { useCallback, useEffect, useRef, useState } from "react";

// 2026-09-20-4b0ab: the one modal moment on this page, drawn by the page. It was
// window.confirm — an unstyled system alert with the origin above it and two buttons named by
// the browser, carrying a three-paragraph warning written for a reader.
//
// THE NATIVE DIALOG ELEMENT, AND THE ORDER IT IS OPENED IN. showModal gives the top layer,
// focus containment and a painted backdrop; the top layer is what a hand-rolled scrim inside
// this page's container-queried grid cannot get right, because it would need a z-index chosen
// against a layout it does not control. But showModal THROWS on an element that already
// carries the open attribute, so the element is rendered WITHOUT it and opened from the effect
// below; the attribute is only the fallback for an environment with no showModal at all (the
// test environment is one: jsdom 25 constructs an HTMLDialogElement and implements neither
// showModal nor close). Written the other way round — render open, call showModal if present —
// the fallback would be the only path a test ever takes while a browser threw, leaving a
// non-modal dialog with no top layer and no focus containment behind green tests.
//
// ESCAPE AND THE BACKDROP ARE THIS COMPONENT'S OWN. A native dialog does not close on a
// backdrop click at all: the backdrop's click event targets the dialog itself, so dismissal is
// a handler comparing the target. And the test environment implements no close watcher and
// fires no cancel event, so Escape needs a key handler of its own; onCancel is kept beside it
// for the browser, where a close watcher DOES fire — without it a browser would close the
// element while this page still believed it was asking, and the promise below would never
// settle. Both paths land on one resolve, which settles once per ask.

export interface ConfirmDialogProps {
  /** The warning to put to the reader, or null when nothing is being asked. Paragraphs are
   *  separated by a blank line, as the rule that writes them separates them. */
  ask: string | null;
  confirmLabel?: string;
  cancelLabel?: string;
  onResolve: (confirmed: boolean) => void;
}

export function ConfirmDialog({
  ask,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  onResolve,
}: ConfirmDialogProps) {
  const held = useRef<HTMLDialogElement | null>(null);
  const settled = useRef(false);

  useEffect(() => {
    settled.current = false;
    const element = held.current;
    if (element === null) return;
    if (typeof element.showModal === "function") element.showModal();
    else element.setAttribute("open", "");
    return () => {
      if (typeof element.close === "function") element.close();
      else element.removeAttribute("open");
    };
  }, [ask]);

  const resolve = useCallback(
    (confirmed: boolean) => {
      if (settled.current) return;
      settled.current = true;
      onResolve(confirmed);
    },
    [onResolve],
  );

  if (ask === null) return null;

  const [question, ...rest] = ask.split("\n\n");
  return (
    <dialog
      ref={held}
      data-testid="confirm-dialog"
      aria-labelledby="confirm-dialog-question"
      className="d-confirm"
      onCancel={(event) => {
        event.preventDefault();
        resolve(false);
      }}
      onKeyDown={(event) => {
        if (event.key !== "Escape") return;
        event.preventDefault();
        resolve(false);
      }}
      onClick={(event) => {
        if (event.target === held.current) resolve(false);
      }}
    >
      <div className="d-confirm-body">
        <p id="confirm-dialog-question" className="d-confirm-t">
          {question}
        </p>
        {rest.map((paragraph, at) => (
          // The paragraphs are a fixed list read out of one string: their order is their
          // identity, and two of them may legitimately read the same.
          <p key={at} className="d-confirm-p">
            {paragraph}
          </p>
        ))}
      </div>
      <div className="d-confirm-foot">
        <button
          type="button"
          data-testid="confirm-dialog-cancel"
          className="btn"
          onClick={() => resolve(false)}
        >
          {cancelLabel}
        </button>
        <button
          type="button"
          data-testid="confirm-dialog-confirm"
          className="btn bad"
          onClick={() => resolve(true)}
        >
          {confirmLabel}
        </button>
      </div>
    </dialog>
  );
}

interface Asked {
  text: string;
  confirmLabel?: string;
  cancelLabel?: string;
}

/** The PROMISE-shaped ask, beside the callback form above. A caller that has to decide inside
 *  a handler it is already running — the config studio's overwrite prompt and the data
 *  archive's restore prompt are both a synchronous decision inside an async file handler, one
 *  of them after the file input has been cleared — reads as it did with window.confirm:
 *  `if (!(await confirmation.ask(warning))) return;`. Without this they would each have to
 *  hold the pending value in state around a callback, which is how a second component gets
 *  written. */
export function useConfirmDialog(): {
  ask: (text: string, labels?: Omit<Asked, "text">) => Promise<boolean>;
  dialog: ConfirmDialogProps;
} {
  const [asked, setAsked] = useState<Asked | null>(null);
  const waiting = useRef<((confirmed: boolean) => void) | null>(null);

  const ask = useCallback(
    (text: string, labels?: Omit<Asked, "text">) =>
      new Promise<boolean>((settle) => {
        // An ask that arrives while one is open supersedes it, and the superseded one is
        // answered no rather than left hanging on a promise nobody will settle.
        waiting.current?.(false);
        waiting.current = settle;
        setAsked({ text, ...labels });
      }),
    [],
  );

  const onResolve = useCallback((confirmed: boolean) => {
    const settle = waiting.current;
    waiting.current = null;
    setAsked(null);
    settle?.(confirmed);
  }, []);

  return {
    ask,
    dialog: {
      ask: asked?.text ?? null,
      confirmLabel: asked?.confirmLabel,
      cancelLabel: asked?.cancelLabel,
      onResolve,
    },
  };
}
