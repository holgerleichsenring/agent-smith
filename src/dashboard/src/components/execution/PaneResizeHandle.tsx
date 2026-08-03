"use client";

// p0395: a vertical drag handle for pane resizing — pointer-captured, so the
// drag keeps tracking when the cursor leaves the handle. The handle only
// reports the pointer's clientX; what dimension that becomes (drawer width,
// rail split) is the caller's mapping.

export function PaneResizeHandle({
  onResize,
  ariaLabel,
  testId,
  className,
}: {
  /** Receives the pointer's clientX for every move while dragging. */
  onResize: (clientX: number) => void;
  ariaLabel: string;
  testId: string;
  className?: string;
}) {
  const onPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    e.preventDefault();
    const handle = e.currentTarget;
    handle.setPointerCapture?.(e.pointerId);
    const move = (ev: PointerEvent) => onResize(ev.clientX);
    const stop = () => {
      handle.removeEventListener("pointermove", move);
      handle.removeEventListener("pointerup", stop);
      handle.removeEventListener("pointercancel", stop);
    };
    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", stop);
    handle.addEventListener("pointercancel", stop);
  };

  return (
    <div
      role="separator"
      aria-orientation="vertical"
      aria-label={ariaLabel}
      data-testid={testId}
      onPointerDown={onPointerDown}
      className={className}
    />
  );
}
