"use client";

import { useState } from "react";

// 2026-09-15-cb3e: what the operator says. One path for everything they write — a design
// message, an answer to a question, a note on a proposal — because the router reads the
// session's state and decides which it is.
// 2026-09-17-042ef: the control and the send button are the studio's own — the text field it
// edits a project in, and its primary button.

export function DialogComposer({
  onSend,
  disabled,
  hint,
}: {
  onSend: (text: string) => void;
  disabled?: boolean;
  /** Why writing is not possible yet — said here rather than left for the router to
   *  answer with the command tutorial a chat channel needs. */
  hint?: string;
}) {
  const [text, setText] = useState("");

  const send = () => {
    const said = text.trim();
    if (said.length === 0 || disabled) return;
    setText("");
    onSend(said);
  };

  return (
    <div className="d-foot flex flex-col gap-1">
      {hint && (
        <p data-testid="dialog-composer-hint" className="dsh-body text-body">
          {hint}
        </p>
      )}
      <div className="flex items-end gap-2">
        <textarea
          data-testid="dialog-composer-text"
          value={text}
          rows={3}
          disabled={disabled}
          placeholder="Keep talking, or answer above…"
          onChange={(event) => setText(event.target.value)}
          onKeyDown={(event) => {
            // Enter sends, shift+enter keeps writing: a design message is often several
            // sentences, and a send on every newline would cut them into turns.
            if (event.key === "Enter" && !event.shiftKey) {
              event.preventDefault();
              send();
            }
          }}
          className="d-input min-w-0 flex-1"
        />
        <button
          type="button"
          data-testid="dialog-composer-send"
          onClick={send}
          disabled={disabled}
          className="btn primary"
        >
          Send
        </button>
      </div>
    </div>
  );
}
