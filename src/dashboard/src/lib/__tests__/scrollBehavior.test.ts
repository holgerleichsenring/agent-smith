import { afterEach, describe, expect, it, vi } from "vitest";
import { scrollBehavior } from "../scrollBehavior";

// 2026-10-17-e730e: a scroll the reader asked for glides unless reduced motion is asked for.

afterEach(() => {
  delete (window as { matchMedia?: unknown }).matchMedia;
});

describe("scrollBehavior", () => {
  it("scrollBehavior_SmoothByDefault_AutoUnderReducedMotion_AutoWithoutMatchMedia", () => {
    expect(scrollBehavior()).toBe("auto");
    window.matchMedia = vi.fn(() => ({ matches: false }) as MediaQueryList);
    expect(scrollBehavior()).toBe("smooth");
    window.matchMedia = vi.fn(() => ({ matches: true }) as MediaQueryList);
    expect(scrollBehavior()).toBe("auto");
  });
});
