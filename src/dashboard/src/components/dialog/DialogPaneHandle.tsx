"use client";

import { useCallback, useEffect, useState, type CSSProperties, type PointerEvent } from "react";

// 2026-11-19-7c42b: the proposal pane is as wide as the operator drags it. It starts at 40% of the page, stays
// between 320px and 70%, and the width is remembered per browser, as the sidebar's collapse is; a double-click on the
// handle goes back to the start. Below the two-column breakpoint the pane sits under the exchange and the handle hides.

const KEY = "agentsmith.dialog-pane-width";
export const PANE_MIN_PX = 320;
export const PANE_MAX_SHARE = 0.7;

/** The width clamped to the pane's bounds within a shell of `shellWidth` pixels. */
export function clampPaneWidth(width: number, shellWidth: number): number {
  return Math.round(Math.max(PANE_MIN_PX, Math.min(width, shellWidth * PANE_MAX_SHARE)));
}

/** The pane width in pixels the operator chose, null for the 40% start; the setter clamps and remembers. */
export function usePaneWidth(): [number | null, (width: number | null) => void] {
  const [width, setWidth] = useState<number | null>(null);
  useEffect(() => {
    try {
      const stored = Number(window.localStorage.getItem(KEY));
      if (stored > 0) setWidth(stored);
    } catch {
      /* starts at 40% */
    }
  }, []);
  const choose = useCallback((next: number | null) => {
    setWidth(next);
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

export function DialogPaneHandle({ onResize }: { onResize: (width: number | null) => void }) {
  const start = (event: PointerEvent<HTMLDivElement>) => {
    const shell = event.currentTarget.parentElement;
    if (!shell) return;
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    const move = (moved: globalThis.PointerEvent) => {
      const box = shell.getBoundingClientRect();
      onResize(clampPaneWidth(box.right - moved.clientX, box.width));
    };
    const stop = () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", stop);
    };
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", stop);
  };
  return (
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label="Resize the proposal pane"
      title="Drag to resize · double-click to reset"
      className="dl-pane-handle"
      data-testid="dialog-pane-handle"
      onPointerDown={start}
      onDoubleClick={() => onResize(null)}
    />
  );
}
