import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RailIdentity } from "../RailIdentity";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import {
  __resetAccessTokenStoreForTests,
  getAccessTokenStore,
} from "@/lib/auth/AccessTokenStore";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-08-25-4530: the rail is the one surface on every route, so it is where
// "who is signed in" belongs. With no authority configured it shows NEITHER a
// name nor a sign-out — that is every installation today.
const session = vi.hoisted(() => ({ signIn: vi.fn(), signOut: vi.fn(), identity: vi.fn() }));
vi.mock("@/lib/auth/session", () => ({
  startAuthSession: () => Promise.resolve(null),
  signIn: session.signIn,
  signOut: session.signOut,
}));
vi.mock("@/lib/identityApi", () => ({ fetchIdentity: () => session.identity() }));

const identity: CallerIdentity = {
  authenticated: true,
  subject: "operator@example",
  issuer: "https://login.example/realm",
  roleClaim: "roles",
  groupClaim: "groups",
  roleClaimValues: ["Operator"],
  groupClaimValues: [],
  roles: ["operator"],
  permissions: ["identity.read"],
  findings: [],
};

function renderRail(authority: string) {
  return render(
    <RuntimeSettingsProvider settings={{ auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority } }}>
      <RailIdentity />
    </RuntimeSettingsProvider>,
  );
}

describe("RailIdentity", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock
  // itself as the teardown hook, which then calls it after the test.
  beforeEach(() => {
    session.signIn.mockReset();
    session.signOut.mockReset();
    session.identity.mockReset();
    session.identity.mockResolvedValue(identity);
  });

  afterEach(() => {
    __resetAccessTokenStoreForTests();
  });

  it("Rail_SignedIn_ShowsTheNameAndSignOut", async () => {
    getAccessTokenStore().hold({ accessToken: "at-1" });

    renderRail("https://login.example/realm");

    // The element renders before the identity read answers — its fallback text is
    // "signed in" — so waiting for the ELEMENT and asserting its content in the
    // same breath races the fetch and loses under load. Wait for the content.
    await waitFor(() =>
      expect(screen.getByTestId("rail-identity-name")).toHaveTextContent("operator@example"),
    );
    fireEvent.click(screen.getByTestId("rail-sign-out"));
    expect(session.signOut).toHaveBeenCalledOnce();
  });

  it("Rail_NoAuthorityConfigured_ShowsNeither", async () => {
    getAccessTokenStore().hold({ accessToken: "at-1" });

    renderRail("");

    await waitFor(() => expect(screen.queryByTestId("rail-identity")).toBeNull());
    expect(screen.queryByTestId("rail-sign-out")).toBeNull();
    expect(screen.queryByTestId("rail-sign-in")).toBeNull();
    // Nothing signs in here, so nothing is asked of the identity endpoint either.
    expect(session.identity).not.toHaveBeenCalled();
  });

  it("Rail_ConfiguredAndSignedOut_OffersTheSignIn", async () => {
    renderRail("https://login.example/realm");

    fireEvent.click(await screen.findByTestId("rail-sign-in"));

    expect(session.signIn).toHaveBeenCalledOnce();
    expect(session.identity).not.toHaveBeenCalled();
  });
});
