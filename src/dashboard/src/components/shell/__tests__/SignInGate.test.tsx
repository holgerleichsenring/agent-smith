import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { AuthRequirements } from "@/lib/authRequirementsApi";
import type { SessionEndReason } from "@/lib/auth/AccessTokenStore";

// 2026-09-21-291b: an enforcing installation used to answer an anonymous caller
// with itself — an empty dashboard, a grey "offline" dot, and a banner accusing
// its own configuration. These cases pin the four ways that must NOT become a
// redirect: a boot still trying, a server that enforces nothing, a session that
// ended, and a sign-out that was asked for.

const server = vi.hoisted(() => ({ enforced: vi.fn<() => AuthRequirements | null>() }));
vi.mock("@/hooks/useAuthRequirements", () => ({
  useAuthRequirements: () => server.enforced(),
}));

const tab = vi.hoisted(() => ({
  token: vi.fn<() => string | null>(),
  ended: vi.fn<() => SessionEndReason | null>(),
  settled: vi.fn<() => boolean>(),
}));
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => tab.token() }));
vi.mock("@/hooks/useSessionEnd", () => ({ useSessionEnd: () => tab.ended() }));
vi.mock("@/hooks/useSignInSettled", () => ({ useSignInSettled: () => tab.settled() }));

const frame = vi.hoisted(() => ({ silent: vi.fn<() => boolean>() }));
vi.mock("@/lib/auth/silentReturnFrame", () => ({
  isSilentReturnFrame: () => frame.silent(),
}));

const authority = vi.hoisted(() => ({ signIn: vi.fn(async () => {}) }));
vi.mock("@/lib/auth/session", () => ({ signIn: () => authority.signIn() }));

import { SignInGate } from "../SignInGate";
import {
  forgetTheAuthorityWasReached,
  rememberTheAuthorityWasReached,
  theAuthorityWasReached,
} from "@/lib/auth/signInSentinel";

const requirements = (enforced: boolean): AuthRequirements => ({
  enforced,
  authority: "https://login.example/tenant",
  audience: null,
  tokenRefusal: null,
  presentedAudience: null,
  presentedIssuer: null,
  presentedTokenVersion: null,
});

function renderGate() {
  return render(
    <SignInGate>
      <p data-testid="the-application">runs</p>
    </SignInGate>,
  );
}

describe("SignInGate", () => {
  // Braces matter: a beforeEach that RETURNS a mock hands vitest the mock itself
  // as a teardown hook.
  beforeEach(() => {
    forgetTheAuthorityWasReached();
    authority.signIn.mockReset();
    server.enforced.mockReset();
    tab.token.mockReset();
    tab.ended.mockReset();
    tab.settled.mockReset();
    frame.silent.mockReset();
    // Enforced, signed out, boot finished, in a real tab: the state the operator
    // photographed. Each case below moves exactly one of these.
    server.enforced.mockReturnValue(requirements(true));
    tab.token.mockReturnValue(null);
    tab.ended.mockReturnValue(null);
    tab.settled.mockReturnValue(true);
    frame.silent.mockReturnValue(false);
  });

  it("SignInGate_EnforcedAndNoTokenOnceTheBootHasFinished_RedirectsOnce", async () => {
    renderGate();

    await waitFor(() => expect(authority.signIn).toHaveBeenCalledTimes(1));
    // Written BEFORE the redirect leaves: one that fails at the authority never
    // comes back to write anything, which is what a loop is made of.
    expect(theAuthorityWasReached()).toBe(true);
    expect(screen.queryByTestId("the-application")).toBeNull();
  });

  it("SignInGate_EnforcedAndTheRedirectReturnedEmpty_RendersTheSignInPageAndDoesNotRedirect", async () => {
    rememberTheAuthorityWasReached();

    renderGate();

    expect(await screen.findByTestId("sign-in-gate-button")).toBeInTheDocument();
    expect(authority.signIn).not.toHaveBeenCalled();
  });

  it("SignInGate_EnforcedAndATokenLands_ForgetsThatItRedirected", async () => {
    rememberTheAuthorityWasReached();
    tab.token.mockReturnValue("a-token");

    renderGate();

    await waitFor(() => expect(theAuthorityWasReached()).toBe(false));
    expect(screen.getByTestId("the-application")).toBeInTheDocument();
  });

  it("SignInGate_EnforcedAndTheSessionExpired_RendersTheSignInPageAndDoesNotRedirect", async () => {
    // An expiry is not a missing beginning. Redirecting would yank somebody out
    // of their work to answer a question they did not ask.
    tab.ended.mockReturnValue("expired");

    renderGate();

    expect(await screen.findByTestId("sign-in-gate")).toHaveTextContent("Your session expired");
    expect(authority.signIn).not.toHaveBeenCalled();
  });

  it("SignInGate_EnforcedAndTheUserSignedOut_DoesNotSignThemBackIn", async () => {
    // signOut() marks the tab, because the DIRECTORY's session usually outlives
    // this one — a redirect would walk the same person straight back in.
    rememberTheAuthorityWasReached();

    renderGate();

    await waitFor(() => expect(screen.getByTestId("sign-in-gate")).toBeInTheDocument());
    expect(authority.signIn).not.toHaveBeenCalled();
  });

  it("SignInGate_EnforcedAndTheBootHasNotFinished_RendersTheApplication", async () => {
    // 2026-08-28-0f46 made the boot settle WITHOUT awaiting the silent attempt,
    // so for a second "no token" means "not yet". Acting there redirects over a
    // sign-in that was about to succeed — and holding the tree back instead would
    // put signinSilent's ten-second timeout in front of every load.
    tab.settled.mockReturnValue(false);

    renderGate();

    expect(screen.getByTestId("the-application")).toBeInTheDocument();
    expect(authority.signIn).not.toHaveBeenCalled();
  });

  it("SignInGate_NotEnforced_NeverRedirectsAndRendersTheApplication", async () => {
    server.enforced.mockReturnValue(requirements(false));

    renderGate();

    expect(screen.getByTestId("the-application")).toBeInTheDocument();
    expect(authority.signIn).not.toHaveBeenCalled();
  });

  it("SignInGate_TheServerDidNotAnswer_RendersTheApplication", async () => {
    // An unreachable requirements route says nothing about this installation,
    // and a gate that guessed would lock people out of a dashboard that enforces
    // nothing.
    server.enforced.mockReturnValue(null);

    renderGate();

    expect(screen.getByTestId("the-application")).toBeInTheDocument();
    expect(authority.signIn).not.toHaveBeenCalled();
  });

  it("SignInGate_InsideASilentReturnFrame_NeverRedirects", async () => {
    // The frame is this whole application, about a second long. A redirect from
    // inside it drives the frame, and the tab waits for an answer nobody delivers.
    frame.silent.mockReturnValue(true);

    renderGate();

    expect(screen.getByTestId("the-application")).toBeInTheDocument();
    expect(authority.signIn).not.toHaveBeenCalled();
  });
});
