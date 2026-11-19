import { act, render, renderHook, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it } from "vitest";
import { TurnQuestions } from "../TurnQuestions";
import { PANE_MAX_SHARE, PANE_MIN_PX, clampPaneWidth, usePaneWidth } from "../DialogPaneHandle";

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
    expect(clampPaneWidth(900, 1000)).toBe(700);
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
});
