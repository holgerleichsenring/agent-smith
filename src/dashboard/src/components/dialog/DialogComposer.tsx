"use client";

import { useEffect, useRef, useState } from "react";
import type { SpecDialogHeldContent } from "@/types/spec-dialog";
import { AttachMenu } from "./AttachMenu";
import { DialogReferenceSelection } from "./DialogReferenceSelection";
import { HeldFileNote } from "./HeldFileNote";
import { heldWhere } from "./singleHeld";
import { sizeOf, type PickLeftOut } from "./referenceSelection";
import type { UploadNote } from "./uploadNote";

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
// 2026-10-01-283db: the second and third kinds the menu was built for — a Website (several files,
// or one .zip the server unpacks) and a Folder (the directory picker, each file under its path).
// 2026-10-02-0d72: and what an upload left — its refusal, or the files a website went without —
// is said HERE, under the control that made it, not on the page-wide failure panel.
// 2026-10-02-075db: a pick of several files or a folder is SHOWN before it is sent — the
// selection card, where the operator ticks what goes — because the server refuses an
// oversized body unread. One file (a .zip the server unpacks, or any single file) goes at once.
// 'Website' became 'Files': since 075da the server keeps any authored file, so the input no
// longer narrows the dialog to website types.
// 2026-10-08-e8b9h: and a pick bigger than what the conversation has LEFT under its byte cap is
// refused here, before a byte is sent, naming both sizes — the server would refuse it anyway.
// Before a conversation exists nothing is known to check, and the first upload opens it.
// 2026-10-09-86e1: the menu is its own component (AttachMenu: keyboard, opens downward when there
// is no room above); Escape gives focus back to the plus. A single picked file the conversation
// already holds is held back with where it is and a "Send anyway"; a larger pick is marked in the
// selection card.

export function DialogComposer({
  onSend,
  onAttach,
  onAttachSite,
  note,
  disabled,
  left = null,
  loadHeld,
}: {
  onSend: (text: string) => void;
  /** An image the operator picked. The conversation keeps it; the next turn is shown it. */
  onAttach: (file: File) => void;
  /** 2026-10-01-283db: a website the operator picked — its files, stored as one set.
   *  2026-10-02-075db: with what the selection card left out, when one was shown. */
  onAttachSite?: (files: File[], leftOut?: PickLeftOut) => void;
  /** What the last upload left: a refusal, or what a stored website went without. */
  note?: UploadNote | null;
  disabled?: boolean;
  /** 2026-10-08-e8b9h: the bytes the conversation may still take; null when none is open yet. */
  left?: number | null;
  /** 2026-10-09-86e1: the content hashes the conversation holds; absent before one is open. */
  loadHeld?: () => Promise<SpecDialogHeldContent[]>;
}) {
  const [text, setText] = useState("");
  const [attaching, setAttaching] = useState(false);
  const picker = useRef<HTMLInputElement>(null);
  const several = useRef<HTMLInputElement>(null);
  const folder = useRef<HTMLInputElement>(null);
  const attach = useRef<HTMLDivElement>(null);
  const plus = useRef<HTMLButtonElement>(null);
  const [heldPick, setHeldPick] = useState<{ file: File; name: string } | null>(null);
  const sendOne = async (file: File) => {
    setHeldPick(null);
    const name = await heldWhere(file, loadHeld);
    if (name) setHeldPick({ file, name });
    else if (onAttachSite && fits([file])) onAttachSite([file]);
  };
  const [pick, setPick] = useState<File[] | null>(null);
  const [over, setOver] = useState<string | null>(null);

  /** Whether `files` fit what is left; says why not when they do not. */
  const fits = (files: File[]) => {
    const bytes = files.reduce((sum, file) => sum + file.size, 0);
    const refused = left !== null && bytes > left;
    setOver(refused ? `${sizeOf(bytes)} is more than the ${sizeOf(left)} this conversation has left. `
      + "Remove an upload in the Uploads tab to make room." : null);
    return !refused;
  };
  const shown: UploadNote | null = over ? { tone: "refused", text: over } : note ?? null;

  const openPicker = (input: HTMLInputElement | null) => {
    setAttaching(false);
    input?.click();
  };

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
      if (event.type === "keydown") plus.current?.focus();
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
              if (picked && fits([picked])) onAttach(picked);
            }}
          />
          {onAttachSite && (
            <>
              <input
                ref={several}
                data-testid="dialog-composer-files"
                type="file"
                multiple
                className="hidden"
                onChange={(event) => pickFiles(event.target, (files) => void sendOne(files[0]), setPick)}
              />
              <input
                ref={folder}
                data-testid="dialog-composer-folder"
                type="file"
                {...{ webkitdirectory: "" }}
                className="hidden"
                onChange={(event) => pickFolder(event.target, setPick)}
              />
            </>
          )}
          <button
            ref={plus}
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
            <AttachMenu
              onLeave={() => setAttaching(false)}
              entries={[
                { key: "image", label: "Image", onPick: () => openPicker(picker.current) },
                ...(onAttachSite ? [
                  { key: "files", label: "Files", onPick: () => openPicker(several.current) },
                  // 2026-10-08-e8b9i: the browser adds its own "upload N files?" prompt to a folder
                  // pick, which no page can restyle or suppress; saying so keeps it from reading as ours.
                  { key: "folder", label: "Folder", onPick: () => openPicker(folder.current),
                    describedBy: "dialog-composer-folder-hint" },
                ] : []),
              ]}
              hint={onAttachSite
                ? { id: "dialog-composer-folder-hint", text: "Your browser may ask to confirm a folder upload." }
                : undefined}
            />
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
      {heldPick && !pick && (
        <HeldFileNote file={heldPick.file} name={heldPick.name} onDismiss={() => setHeldPick(null)}
          onSendAnyway={() => {
            setHeldPick(null);
            if (onAttachSite && fits([heldPick.file])) onAttachSite([heldPick.file]);
          }} />
      )}
      {pick && onAttachSite && (
        <DialogReferenceSelection
          files={pick}
          left={left}
          loadHeld={loadHeld}
          onCancel={() => setPick(null)}
          onSend={(files, leftOut) => {
            setPick(null);
            onAttachSite(files, leftOut);
          }}
        />
      )}
      {shown && !pick && !heldPick && (
        <p
          role={shown.tone === "refused" ? "alert" : "status"}
          data-testid="dialog-composer-upload-note"
          data-tone={shown.tone}
          className={`d-upload-note${shown.tone === "refused" ? " bad" : ""}`}
        >
          {shown.text}
        </p>
      )}
    </div>
  );
}

/** Files picked: one goes at once — a .zip is what its author packed — several are shown first.
 *  The input is cleared so the same pick can be made again. */
function pickFiles(
  input: HTMLInputElement,
  onAttachSite: (files: File[]) => unknown,
  show: (files: File[]) => void,
) {
  const picked = Array.from(input.files ?? []);
  input.value = "";
  if (picked.length === 1) onAttachSite(picked);
  else if (picked.length > 1) show(picked);
}

/** A folder picked: always shown first, whatever it holds. */
function pickFolder(input: HTMLInputElement, show: (files: File[]) => void) {
  const picked = Array.from(input.files ?? []);
  input.value = "";
  if (picked.length > 0) show(picked);
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
