"use client";

import { useLayoutEffect, useRef, useState, type KeyboardEvent } from "react";

// 2026-10-09-86e1: the plus's menu, out of the composer. It opens UPWARD where there is room and
// DOWNWARD where there is not — measured against the viewport once it is drawn — so an empty
// conversation, whose composer sits near the top of the page, shows every entry. "Room" is
// what neither the viewport nor a scrolling or clipping ancestor (the page's main region) cuts. It takes focus
// on its first entry and is walked with the arrow keys, Home and End, as a menu is; Tab leaves it.

export interface AttachEntry {
  key: string;
  label: string;
  onPick: () => void;
  describedBy?: string;
}

export function AttachMenu({ entries, hint, onLeave }: {
  entries: AttachEntry[];
  /** A line under the entries, referenced by an entry's describedBy. */
  hint?: { id: string; text: string };
  /** The menu was left by Tab: it closes without taking focus anywhere. */
  onLeave: () => void;
}) {
  const menu = useRef<HTMLDivElement>(null);
  const [down, setDown] = useState(false);
  const [room, setRoom] = useState<number | null>(null);
  useLayoutEffect(() => {
    const drawn = menu.current?.getBoundingClientRect();
    const clip = menu.current ? clipOf(menu.current) : null;
    if (drawn && clip && drawn.top < clip.top) setDown(true);
    // A narrow page: the menu would run past the right edge, so it is narrowed to what is left
    // and its entries wrap — it never spills into the column beside it.
    if (drawn && clip && drawn.right > clip.right) setRoom(Math.max(96, clip.right - drawn.left - 8));
    menu.current?.querySelector<HTMLButtonElement>("[role=menuitem]")?.focus();
  }, []);

  const walk = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === "Tab") return onLeave();
    const items = [...(menu.current?.querySelectorAll<HTMLButtonElement>("[role=menuitem]") ?? [])];
    const at = items.indexOf(document.activeElement as HTMLButtonElement);
    const next = { ArrowDown: at + 1, ArrowUp: at - 1, Home: 0, End: items.length - 1 }[event.key];
    if (next === undefined || items.length === 0) return;
    event.preventDefault();
    items[(next + items.length) % items.length].focus();
  };

  return (
    <div ref={menu} className={["d-menu", down && "down", room !== null && "narrow"].filter(Boolean).join(" ")}
      style={room !== null ? { maxWidth: room, minWidth: 0 } : undefined} role="menu" data-testid="dialog-composer-attach-menu"
      data-direction={down ? "down" : "up"} onKeyDown={walk}>
      {entries.map((entry) => (
        <button key={entry.key} type="button" role="menuitem" data-testid={`dialog-composer-attach-${entry.key}`}
          aria-describedby={entry.describedBy} onClick={entry.onPick} className="d-menu-item">
          {entry.label}
        </button>
      ))}
      {hint && (
        <span id={hint.id} className="d-menu-sub" data-testid="dialog-composer-folder-hint">{hint.text}</span>
      )}
    </div>
  );
}

/** The box nothing clips the menu inside: the viewport, narrowed by every clipping ancestor. */
function clipOf(element: HTMLElement): { top: number; right: number } {
  let top = 0;
  let right = window.innerWidth;
  for (let at = element.parentElement; at; at = at.parentElement) {
    const style = getComputedStyle(at);
    const box = at.getBoundingClientRect();
    if (style.overflowY !== "visible") top = Math.max(top, box.top);
    if (style.overflowX !== "visible") right = Math.min(right, box.right);
  }
  return { top, right };
}
