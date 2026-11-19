"use client";

import { useCallback, useEffect, useRef, useState, type CSSProperties, type KeyboardEvent, type PointerEvent } from "react";

// 2026-11-19-7c42b: the proposal pane is as wide as the operator drags it. It starts at 40% of the page, stays
// between 320px and 70% of what the conversations' bar leaves, and the width is remembered per browser, as the
// sidebar's collapse is; a double-click on the handle goes back to the start, and the arrow keys move it. The CSS
// clamps the same bounds, so a width saved on a wider screen never squeezes the exchange. Below the two-column
// breakpoint the pane sits under the exchange and the handle hides.

const KEY = "agentsmith.dialog-pane-width";
export const PANE_MIN_PX = 320;
export const PANE_MAX_SHARE = 0.7;
/** The conversations' bar and the handle, which the pane's share is not taken from. */
export const PANE_BESIDE_PX = 245;
const KEY_STEP_PX = 32;

/** The width clamped to the pane's bounds within a shell of `shellWidth` pixels. */
export function clampPaneWidth(width: number, shellWidth: number): number {
  const most = Math.max(PANE_MIN_PX, (shellWidth - PANE_BESIDE_PX) * PANE_MAX_SHARE);
  return Math.round(Math.max(PANE_MIN_PX, Math.min(width, most)));
}

/** The pane width in pixels the operator chose, null for the 40% start; `remember` keeps it for the next visit. */
export function usePaneWidth(): [number | null, (width: number | null, remember?: boolean) => void] {
  const [width, setWidth] = useState<number | null>(null);
  useEffect(() => {
    try {
      const stored = Number(window.localStorage.getItem(KEY));
      if (stored > 0) setWidth(stored);
    } catch {
      /* starts at 40% */
    }
  }, []);
  const choose = useCallback((next: number | null, remember = true) => {
    setWidth(next);
    if (!remember) return;
    try {
      if (next === null) window.localStorage.removeItem(KEY);
      else window.localStorage.setItem(KEY, String(next));
    } catch {
      /* kept for this page only */
    }
  }, []);
  return [width, choose];
}

/** The shell's style carrying the pane width, read by the grid template. */
export function paneWidthStyle(width: number | null): CSSProperties | undefined {
  return width === null ? undefined : ({ "--dl-pane-w": `${width}px` } as CSSProperties);
}

export function DialogPaneHandle({ onResize }: { onResize: (width: number | null, remember?: boolean) => void }) {
  const dragged = useRef<{ moved: boolean; width: number } | null>(null);
  const shellOf = (handle: HTMLElement) => handle.parentElement?.getBoundingClientRect() ?? null;
  const widthAt = (handle: HTMLElement, clientX: number) => {
    const box = shellOf(handle);
    return box ? clampPaneWidth(box.right - clientX, box.width) : null;
  };
  // The pane's track — everything right of the handle, its margins included — which is what --dl-pane-w sets.
  const paneWidth = (handle: HTMLElement) => {
    const box = shellOf(handle);
    return box ? box.right - handle.getBoundingClientRect().right : PANE_MIN_PX;
  };
  const start = (event: PointerEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    dragged.current = { moved: false, width: paneWidth(event.currentTarget) };
  };
  const move = (event: PointerEvent<HTMLDivElement>) => {
    if (dragged.current === null) return;
    const width = widthAt(event.currentTarget, event.clientX);
    if (width === null) return;
    dragged.current = { moved: true, width };
    onResize(width, false);
  };
  // Released or lost (a touch cancel, an unmount): the width reached is kept once, not on every move; a click that
  // moved nothing keeps nothing, so the 40% start stays until the operator really drags.
  const end = () => {
    if (dragged.current?.moved) onResize(dragged.current.width, true);
    dragged.current = null;
  };
  const key = (event: KeyboardEvent<HTMLDivElement>) => {
    const step = event.key === "ArrowLeft" ? KEY_STEP_PX : event.key === "ArrowRight" ? -KEY_STEP_PX : 0;
    if (step === 0) return;
    event.preventDefault();
    const box = shellOf(event.currentTarget);
    if (box) onResize(clampPaneWidth(paneWidth(event.currentTarget) + step, box.width), true);
  };
  return (
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label="Resize the proposal pane"
      title="Drag to resize · double-click to reset"
      tabIndex={0}
      className="dl-pane-handle"
      data-testid="dialog-pane-handle"
      onPointerDown={start}
      onPointerMove={move}
      onPointerUp={end}
      onLostPointerCapture={end}
      onKeyDown={key}
      onDoubleClick={() => onResize(null, true)}
    />
  );
}
