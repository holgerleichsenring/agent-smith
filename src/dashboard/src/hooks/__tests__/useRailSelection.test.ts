import { renderHook, act } from "@testing-library/react";
import { describe, it, expect, beforeEach } from "vitest";
import { useRailSelection, defaultSelection } from "../useRailSelection";

describe("useRailSelection", () => {
  beforeEach(() => {
    window.history.replaceState(null, "", "/jobs/run-1");
  });

  it("useRailSelection_DefaultsToFailedStep_WhenNoHash", () => {
    const items = [
      { id: "step-0", status: "ok" },
      { id: "step-1", status: "ok" },
      { id: "step-14", status: "fail" },
      { id: "result", status: "fail" },
    ];

    const { result } = renderHook(() => useRailSelection(items));

    expect(result.current.selected).toBe("step-14");
  });

  it("useRailSelection_NoFailure_DefaultsToLastNode", () => {
    const items = [
      { id: "step-0", status: "ok" },
      { id: "result", status: "ok" },
    ];

    expect(defaultSelection(items)).toBe("result");
  });

  it("useRailSelection_SelectAndExpand_WritesHash", () => {
    const items = [{ id: "step-9", status: "ok" }, { id: "sub-x", status: "ok" }];
    const { result } = renderHook(() => useRailSelection(items));

    act(() => result.current.select("sub-x", "step-9"));

    expect(result.current.selected).toBe("sub-x");
    expect(result.current.expanded.has("step-9")).toBe(true);
    expect(window.location.hash).toContain("n=sub-x");
    expect(window.location.hash).toContain("step-9");
  });

  it("useRailSelection_Toggle_AddsAndRemovesExpansion", () => {
    // step-9 is the default (last, no failure) so it is pre-expanded; toggle a
    // different node to assert add/remove from a known-empty starting state.
    const items = [{ id: "step-0", status: "ok" }, { id: "step-9", status: "ok" }];
    const { result } = renderHook(() => useRailSelection(items));

    act(() => result.current.toggle("step-0"));
    expect(result.current.expanded.has("step-0")).toBe(true);

    act(() => result.current.toggle("step-0"));
    expect(result.current.expanded.has("step-0")).toBe(false);
  });
});
