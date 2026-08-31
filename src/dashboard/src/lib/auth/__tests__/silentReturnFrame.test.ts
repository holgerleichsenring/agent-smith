import { describe, it, expect, afterEach } from "vitest";
import { isSilentReturnFrame } from "../silentReturnFrame";

// 2026-08-28-0f46: the frame lives about a second and boots the whole
// application. Telling it apart from the tab that owns the sign-in is what lets
// the expensive parts of that boot stay out of it.

function embeddedIn(parent: unknown): void {
  Object.defineProperty(window, "parent", { value: parent, configurable: true });
}

afterEach(() => {
  embeddedIn(window.self);
  window.history.replaceState({}, "", "/");
});

describe("isSilentReturnFrame", () => {
  it("Frame_TheTabThatOwnsTheSignIn_IsNotOne", () => {
    window.history.replaceState({}, "", "/signin-callback?code=the-code&state=the-state");

    expect(isSilentReturnFrame()).toBe(false);
  });

  it("Frame_EmbeddedAndCarryingTheAuthoritysAnswer_IsOne", () => {
    embeddedIn({});
    window.history.replaceState({}, "", "/signin-callback?code=the-code&state=the-state");

    expect(isSilentReturnFrame()).toBe(true);
  });

  it("Frame_EmbeddedAndCarryingARefusal_IsStillOne", () => {
    embeddedIn({});
    window.history.replaceState({}, "", "/signin-callback?error=login_required&state=the-state");

    expect(isSilentReturnFrame()).toBe(true);
  });

  it("Frame_EmbeddedForSomeOtherReason_IsNotOne", () => {
    // A dashboard someone embedded in their own page is not a sign-in frame,
    // and everything it renders it is meant to render.
    embeddedIn({});
    window.history.replaceState({}, "", "/jobs");

    expect(isSilentReturnFrame()).toBe(false);
  });
});
