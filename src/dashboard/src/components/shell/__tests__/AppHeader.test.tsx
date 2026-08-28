import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { AppHeader } from "../AppHeader";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import {
  __resetAccessTokenStoreForTests,
  getAccessTokenStore,
} from "@/lib/auth/AccessTokenStore";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-08-27-1ed6: the header is on every route. It says where you are, opens
// configuration in one click, and carries the account — which keeps the two properties
// the rail identity had before it moved here: silence where no authority is configured,
// and hooks that mount only for an installation that has one.
const session = vi.hoisted(() => ({ signIn: vi.fn(), signOut: vi.fn(), identity: vi.fn() }));
vi.mock("@/lib/auth/session", () => ({
  startAuthSession: () => Promise.resolve(null),
  signIn: session.signIn,
  signOut: session.signOut,
}));
vi.mock("@/lib/identityApi", () => ({ fetchIdentity: () => session.identity() }));

const usePathname = vi.fn(() => "/");
vi.mock("next/navigation", () => ({ usePathname: () => usePathname() }));

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

function renderHeader(authority = "") {
  return render(
    <RuntimeSettingsProvider settings={{ auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority } }}>
      <AppHeader />
    </RuntimeSettingsProvider>,
  );
}

describe("AppHeader", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock itself as the
  // teardown hook, which then calls it after the test.
  beforeEach(() => {
    usePathname.mockReturnValue("/");
    session.signIn.mockReset();
    session.signOut.mockReset();
    session.identity.mockReset();
    session.identity.mockResolvedValue(identity);
  });

  afterEach(() => {
    __resetAccessTokenStoreForTests();
  });

  it("Header_TheCurrentPath_NamesThePlaceTheTableNamesIt", () => {
    renderHeader();
    expect(screen.getByTestId("app-header-place")).toHaveTextContent("Runs");

    usePathname.mockReturnValue("/config/settings/orchestrator");
    renderHeader();
    expect(screen.getAllByTestId("app-header-place")[1]).toHaveTextContent("Orchestrator");
  });

  it("Header_APathNotInTheTable_RendersNoPlaceRatherThanGuessing", () => {
    usePathname.mockReturnValue("/something-nobody-named");
    renderHeader();

    expect(screen.getByTestId("app-header")).toBeInTheDocument();
    expect(screen.queryByTestId("app-header-place")).toBeNull();
  });

  it("Header_TheGear_LinksToConfiguration", () => {
    renderHeader();

    const gear = screen.getByTestId("app-header-gear");
    expect(gear).toHaveAttribute("href", "/config");
    expect(gear).toHaveAttribute("aria-label", "Configuration");
    // Drawn, not a text glyph the operator's emoji font would render for us.
    expect(gear.querySelector("svg")).not.toBeNull();
  });

  it("Header_NoAuthorityConfigured_RendersNoAccountAndMountsNoIdentityHook", async () => {
    getAccessTokenStore().hold({ accessToken: "at-1" });

    renderHeader("");

    await waitFor(() => expect(screen.queryByTestId("header-identity")).toBeNull());
    expect(screen.queryByTestId("header-sign-in")).toBeNull();
    // Nothing signs in here, so nothing is asked of the identity endpoint either.
    expect(session.identity).not.toHaveBeenCalled();
  });

  it("Header_AnAuthorityAndNoToken_OffersSignIn", async () => {
    renderHeader("https://login.example/realm");

    fireEvent.click(await screen.findByTestId("header-sign-in"));

    expect(session.signIn).toHaveBeenCalledOnce();
    expect(session.identity).not.toHaveBeenCalled();
  });

  it("Header_SignedIn_ShowsTheNameAndOffersSignOut", async () => {
    getAccessTokenStore().hold({ accessToken: "at-1" });

    renderHeader("https://login.example/realm");

    // The element renders before the identity read answers — its fallback text is
    // "signed in" — so waiting for the ELEMENT and asserting its content in the same
    // breath races the fetch and loses under load. Wait for the content.
    await waitFor(() =>
      expect(screen.getByTestId("header-identity-name")).toHaveTextContent("operator@example"),
    );
    expect(screen.queryByTestId("header-account-menu")).toBeNull();

    fireEvent.click(screen.getByTestId("header-account"));
    expect(screen.getByTestId("header-identity-link")).toHaveAttribute("href", "/identity");

    fireEvent.click(screen.getByTestId("header-sign-out"));
    expect(session.signOut).toHaveBeenCalledOnce();
  });
});
