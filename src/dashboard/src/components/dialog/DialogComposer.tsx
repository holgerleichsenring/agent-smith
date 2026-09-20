"use client";

import { useRef, useState } from "react";

// 2026-09-15-cb3e: what the operator says. One path for everything they write — a design
// message, an answer to a question, a note on a proposal — because the router reads the
// session's state and decides which it is.
// 2026-09-17-042ef: the control and the send button are the studio's own — the text field it
// edits a project in, and its primary button.
// 2026-09-20-3af8: and an image can go beside what is typed. It is stored the moment it is
// picked rather than held until Send: there is one upload either way, and an image kept in the
// page would be lost by the reload that a design turn invites while it runs for a minute.

export function DialogComposer({
  onSend,
  onAttach,
  disabled,
  hint,
}: {
  onSend: (text: string) => void;
  /** An image the operator picked. The conversation keeps it; the next turn is shown it. */
  onAttach: (file: File) => void;
  disabled?: boolean;
  /** Why writing is not possible yet — said here rather than left for the router to
   *  answer with the command tutorial a chat channel needs. */
  hint?: string;
}) {
  const [text, setText] = useState("");
  const picker = useRef<HTMLInputElement>(null);

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
        {/* The input itself is never shown: a file control styled by the browser cannot be
            made to read as this page's own, and the button beside Send can. */}
        <input
          ref={picker}
          data-testid="dialog-composer-image"
          type="file"
          accept="image/png,image/jpeg,image/gif,image/webp"
          className="hidden"
          onChange={(event) => {
            const picked = event.target.files?.[0];
            event.target.value = "";
            if (picked) onAttach(picked);
          }}
        />
        <button
          type="button"
          data-testid="dialog-composer-attach"
          title="Attach an image"
          onClick={() => picker.current?.click()}
          disabled={disabled}
          className="btn"
        >
          Image
        </button>
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
