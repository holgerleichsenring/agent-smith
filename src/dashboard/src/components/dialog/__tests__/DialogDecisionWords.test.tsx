import { act, fireEvent, render, renderHook, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TurnQuestions } from "../TurnQuestions";
import { DialogPaneHandle, PANE_MAX_SHARE, PANE_MIN_PX, clampPaneWidth, usePaneWidth } from "../DialogPaneHandle";

// 2026-11-19-7c42b: the proposal under a question is the agent's own sentence, and the pane is as wide as dragged.

const KEY = "agentsmith.dialog-pane-width";

describe("dialog decision words", () => {
  // This jsdom build ships no localStorage; a Map stands in for it, as in AppSidebar.test.
  const backing = new Map<string, string>();
  beforeEach(() => {
    backing.clear();
    Object.defineProperty(window, "localStorage", {
      configurable: true,
      value: {
        getItem: (k: string) => backing.get(k) ?? null,
        setItem: (k: string, v: string) => void backing.set(k, v),
        removeItem: (k: string) => void backing.delete(k),
      },
    });
  });

  it("TurnQuestions_Proposal_HasNoLabel", () => {
    render(<TurnQuestions questions={[{ question: "Keep CSV?", proposal: "Yes, behind a flag", specIds: [] }]} />);

    expect(screen.getByTestId("turn-question-proposal")).toHaveTextContent(/^Yes, behind a flag$/);
    expect(screen.getByTestId("turn-question")).not.toHaveTextContent("Proposal:");
  });

  it("DialogPaneWidth_Drag_ClampsAndRemembers", () => {
    expect(PANE_MIN_PX).toBe(320);
    expect(PANE_MAX_SHARE).toBe(0.7);
    expect(clampPaneWidth(100, 1000)).toBe(320);
    // 70% of what the conversations' bar and the handle leave: (1000 - 245) * 0.7 = 528.5.
    expect(clampPaneWidth(900, 1000)).toBe(529);
    expect(clampPaneWidth(500.4, 1000)).toBe(500);

    const { result } = renderHook(() => usePaneWidth());
    expect(result.current[0]).toBeNull();
    act(() => result.current[1](480));
    expect(result.current[0]).toBe(480);
    expect(window.localStorage.getItem(KEY)).toBe("480");

    const reread = renderHook(() => usePaneWidth());
    expect(reread.result.current[0]).toBe(480);

    act(() => result.current[1](null));
    expect(result.current[0]).toBeNull();
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it("DialogPaneWidth_DuringADrag_IsNotStoredUntilRelease", () => {
    const { result } = renderHook(() => usePaneWidth());
    act(() => result.current[1](600, false));
    expect(result.current[0]).toBe(600);
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });

  it("DialogPaneHandle_Keyboard_ResizesAndRemembers", () => {
    // A 1600px shell whose handle ends 600px from its right edge: the pane's track is 600px.
    const onResize = vi.fn();
    render(<div data-testid="shell"><DialogPaneHandle onResize={onResize} /><aside /></div>);
    const handle = screen.getByTestId("dialog-pane-handle");
    const shell = screen.getByTestId("shell");
    shell.getBoundingClientRect = () => ({ right: 1600, width: 1600 }) as DOMRect;
    handle.getBoundingClientRect = () => ({ right: 1000 }) as DOMRect;
    expect(handle).toHaveAttribute("tabindex", "0");

    fireEvent.keyDown(handle, { key: "ArrowLeft" });
    expect(onResize).toHaveBeenLastCalledWith(632, true);
    fireEvent.keyDown(handle, { key: "ArrowRight" });
    expect(onResize).toHaveBeenLastCalledWith(568, true);
    fireEvent.doubleClick(handle);
    expect(onResize).toHaveBeenLastCalledWith(null, true);
  });

  it("DialogPaneHandle_AClickWithoutMoving_KeepsNothing", () => {
    const onResize = vi.fn();
    render(<div><DialogPaneHandle onResize={onResize} /><aside /></div>);
    const handle = screen.getByTestId("dialog-pane-handle");
    handle.setPointerCapture = () => {};
    fireEvent.pointerDown(handle, { pointerId: 1, clientX: 900 });
    fireEvent.pointerUp(handle, { pointerId: 1, clientX: 900 });
    expect(onResize).not.toHaveBeenCalled();
  });
});
