"use client";

import { useEffect, useRef, useState } from "react";

// 2026-09-15-cb3e: what the operator says. One path for everything they write — a design
// message, an answer to a question, a note on a proposal — because the router reads the
// session's state and decides which it is.
// 2026-09-17-042ef: the control and the send button are the studio's own — the text field it
// edits a project in, and its primary button.
// 2026-09-20-3af8: and an image can go beside what is typed. It is stored the moment it is
// picked rather than held until Send: there is one upload either way, and an image kept in the
// page would be lost by the reload that a design turn invites while it runs for a minute.
// 2026-09-23-6e3f: and it no longer explains why it is disabled. The one reason it carried a
// hint was a conversation with no project picked, and that state does not render a composer at
// all now — the exchange column is the choice instead.
// 2026-09-20-4b0ac: the two controls are glyphs where every other composer puts them — a plus
// at the lower left, opening a small menu of what can be attached, and an arrow at the lower
// right. The plus opens a MENU rather than the file dialog, because a plus that always opens
// the same picker is an image button wearing a plus, and the second attachable kind should not
// need this control redesigned again. Both are drawn in mock-parity.css: this directory's own
// guard forbids drawing a surface out of theme utilities.

export function DialogComposer({
  onSend,
  onAttach,
  disabled,
}: {
  onSend: (text: string) => void;
  /** An image the operator picked. The conversation keeps it; the next turn is shown it. */
  onAttach: (file: File) => void;
  disabled?: boolean;
}) {
  const [text, setText] = useState("");
  const [attaching, setAttaching] = useState(false);
  const picker = useRef<HTMLInputElement>(null);
  const attach = useRef<HTMLDivElement>(null);

  const send = () => {
    const said = text.trim();
    if (said.length === 0 || disabled) return;
    setText("");
    onSend(said);
  };

  // A menu that outlives what opened it is a menu the operator has to hunt for a way out of.
  // Escape and a press outside both close it; the listeners exist only while it is open, and
  // the press is watched on the way DOWN so a control under it is not acted on first.
  useEffect(() => {
    if (!attaching) return;
    // A composer that has BECOME disabled while the menu stands open is the one way an entry
    // could reach the picker after writing stopped being possible — the hidden input is not
    // itself disabled, and nothing else would press the menu away. The menu goes with it.
    if (disabled) {
      setAttaching(false);
      return;
    }
    const leave = (event: Event) => {
      if (event instanceof KeyboardEvent && event.key !== "Escape") return;
      if (event.type === "mousedown" && attach.current?.contains(event.target as Node)) return;
      setAttaching(false);
    };
    document.addEventListener("keydown", leave);
    document.addEventListener("mousedown", leave);
    return () => {
      document.removeEventListener("keydown", leave);
      document.removeEventListener("mousedown", leave);
    };
  }, [attaching, disabled]);

  return (
    <div className="d-foot">
      <div className="flex items-end gap-2">
        <div className="d-attach" ref={attach}>
          {/* The input itself is never shown: a file control styled by the browser cannot be
              made to read as this page's own, and a menu entry can. */}
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
            aria-label="Attach"
            title="Attach"
            aria-haspopup="menu"
            aria-expanded={attaching}
            onClick={() => setAttaching(!attaching)}
            disabled={disabled}
            className="d-icon"
          >
            <PlusGlyph />
          </button>
          {attaching && (
            <div className="d-menu" role="menu" data-testid="dialog-composer-attach-menu">
              <button
                type="button"
                role="menuitem"
                data-testid="dialog-composer-attach-image"
                onClick={() => {
                  setAttaching(false);
                  picker.current?.click();
                }}
                className="d-menu-item"
              >
                Image
              </button>
            </div>
          )}
        </div>
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
          aria-label="Send"
          title="Send"
          onClick={send}
          disabled={disabled}
          className="d-icon send"
        >
          <ArrowGlyph />
        </button>
      </div>
    </div>
  );
}

// The two glyphs, drawn to the shape every ported mock surface uses — a 16-unit box, no fill,
// the current colour as stroke, out of the accessibility tree because the button is named.
// lucide is a dependency and is deliberately not reached for: it is confined to the run-status
// icon, and a packaged glyph beside a hand-drawn one is visible.
function PlusGlyph() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinecap="round"
      aria-hidden="true"
    >
      <path d="M8 3.2v9.6M3.2 8h9.6" />
    </svg>
  );
}

function ArrowGlyph() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M8 13V3.4M3.6 7.8 8 3.2l4.4 4.6" />
    </svg>
  );
}
