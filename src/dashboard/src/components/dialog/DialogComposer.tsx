"use client";

import { useState } from "react";

// 2026-09-15-cb3e: what the operator says. One path for everything they write — a design
// message, an answer to a question, a note on a proposal — because the router reads the
// session's state and decides which it is.

export function DialogComposer({
  onSend,
  disabled,
}: {
  onSend: (text: string) => void;
  disabled?: boolean;
}) {
  const [text, setText] = useState("");

  const send = () => {
    const said = text.trim();
    if (said.length === 0 || disabled) return;
    setText("");
    onSend(said);
  };

  return (
    <div className="flex gap-2">
      <textarea
        data-testid="dialog-composer-text"
        value={text}
        rows={3}
        disabled={disabled}
        placeholder="Describe what you want built — or answer the question above."
        onChange={(event) => setText(event.target.value)}
        onKeyDown={(event) => {
          // Enter sends, shift+enter keeps writing: a design message is often several
          // sentences, and a send on every newline would cut them into turns.
          if (event.key === "Enter" && !event.shiftKey) {
            event.preventDefault();
            send();
          }
        }}
        className="flex-1 rounded border border-stone-300 px-3 py-2 dsh-body text-stone-700"
      />
      <button
        type="button"
        data-testid="dialog-composer-send"
        onClick={send}
        disabled={disabled}
        className="self-end rounded bg-emerald-700 px-3 py-2 text-sm text-white hover:bg-emerald-800 disabled:bg-stone-300"
      >
        Send
      </button>
    </div>
  );
}
