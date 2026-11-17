import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { HubConnectionState } from "@microsoft/signalr";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import { __resetAccessTokenStoreForTests, getAccessTokenStore } from "@/lib/auth/AccessTokenStore";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-11-16-0440c: the profile at the sidebar's foot. It holds every setting the sidebar does
// not (configuration, access, installation, system) and the account, keeping the two properties
// the header account had: silence where no authority is configured, and identity hooks that mount
// only for an installation that has one; an ended session reads differently from no session.

const session = vi.hoisted(() => ({ signIn: vi.fn(), signOut: vi.fn(), identity: vi.fn(), connection: 1 }));
vi.mock("@/lib/auth/session", () => ({
  startAuthSession: () => Promise.resolve(null),
  signIn: session.signIn,
  signOut: session.signOut,
}));
vi.mock("@/lib/identityApi", () => ({ fetchIdentity: () => session.identity() }));
vi.mock("@/hooks/useJobsHub", () => ({ useJobsHub: () => ({ connectionState: session.connection }) }));

import { ProfileMenu } from "../ProfileMenu";

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
  findings: [], nameIsReadable: true,
};

function renderProfile(authority = "") {
  return render(
    <RuntimeSettingsProvider settings={{ auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority } }}>
      <ProfileMenu collapsed={false} />
    </RuntimeSettingsProvider>,
  );
}

const open = () => fireEvent.click(screen.getByTestId("profile-button"));

describe("ProfileMenu", () => {
  beforeEach(() => {
    session.signIn.mockReset();
    session.signOut.mockReset();
    session.identity.mockReset().mockResolvedValue(identity);
    session.connection = HubConnectionState.Connected;
  });

  afterEach(() => __resetAccessTokenStoreForTests());

  it("Profile_Menu_HoldsSettingsAccessInstallationAndSystem", () => {
    renderProfile();
    expect(screen.queryByTestId("profile-menu")).toBeNull();
    open();
    expect(screen.getByTestId("profile-settings")).toHaveAttribute("href", "/config");
    expect(screen.getByTestId("profile-access")).toHaveAttribute("href", "/config/access");
    expect(screen.getByTestId("profile-installation")).toHaveAttribute("href", "/config/installation");
    expect(screen.getByTestId("profile-system")).toHaveAttribute("href", "/system");
  });

  it("Profile_EscapeClosesTheMenu", () => {
    renderProfile();
    open();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByTestId("profile-menu")).toBeNull();
  });

  it("Profile_NoAuthority_NamesTheInstallation_NoAccountAndNoIdentityRead", async () => {
    renderProfile();
    expect(screen.getByTestId("profile-name")).toHaveTextContent("This installation");
    open();
    expect(screen.queryByTestId("profile-sign-in")).toBeNull();
    expect(screen.queryByTestId("profile-sign-out")).toBeNull();
    await Promise.resolve();
    expect(session.identity).not.toHaveBeenCalled();
  });

  it("Profile_TheHubConnection_IsTheDotAndTheWord", () => {
    session.connection = HubConnectionState.Disconnected;
    renderProfile();
    expect(screen.getByTestId("profile-status")).toHaveTextContent("Offline");
    expect(screen.getByTestId("profile-connection").className).not.toContain("on");
  });

  it("Profile_AnAuthorityAndNoToken_OffersSignIn", () => {
    renderProfile("https://login.example/realm");
    expect(screen.getByTestId("profile-name")).toHaveTextContent("Not signed in");
    open();
    fireEvent.click(screen.getByTestId("profile-sign-in"));
    expect(session.signIn).toHaveBeenCalledOnce();
  });

  it("Profile_AnExpiredSession_SaysWhyItEnded", async () => {
    getAccessTokenStore().end("expired");
    renderProfile("https://login.example/realm");
    expect(await screen.findByText("Your session expired")).toBeInTheDocument();
  });

  // 2026-11-17-d6fbe: restored from the deleted AppHeader suite (2026-08-28-0f46) — a refused renewal, a tab that
  // never signed in and a sign-in after an expiry each read as what they are.
  it("Profile_ARefusedRenewal_SaysTheSessionEnded", async () => {
    getAccessTokenStore().end("renewal-refused");
    renderProfile("https://login.example/realm");
    expect(await screen.findByTestId("profile-status")).toHaveTextContent("Your session ended");
  });

  it("Profile_NeverSignedIn_ReadsDifferentlyFromExpired", async () => {
    renderProfile("https://login.example/realm");
    expect(await screen.findByTestId("profile-name")).toHaveTextContent("Not signed in");
    // Nothing ended here; saying one did would report a fault at a tab that has simply not signed in yet.
    expect(screen.getByTestId("profile-status")).not.toHaveTextContent(/session (expired|ended)/);
    expect(screen.getByTestId("profile-status")).toHaveTextContent("Connected");
  });

  it("Profile_SignedInAfterAnExpiry_StopsSayingItEnded", async () => {
    getAccessTokenStore().end("expired");
    renderProfile("https://login.example/realm");
    await screen.findByText("Your session expired");

    act(() => getAccessTokenStore().hold({ accessToken: "at-1" }));

    await waitFor(() => expect(screen.queryByText("Your session expired")).toBeNull());
    await waitFor(() => expect(screen.getByTestId("profile-name")).toHaveTextContent("operator@example"));
  });

  it("Profile_SignedIn_ShowsTheNameAndRole_AndOffersIdentityAndSignOut", async () => {
    getAccessTokenStore().hold({ accessToken: "at-1" });
    renderProfile("https://login.example/realm");

    // The element renders before the identity read answers; wait for the content.
    await waitFor(() => expect(screen.getByTestId("profile-name")).toHaveTextContent("operator@example"));
    expect(screen.getByTestId("profile-status")).toHaveTextContent("Connected · operator");
    open();
    expect(screen.getByTestId("profile-identity")).toHaveAttribute("href", "/identity");
    fireEvent.click(screen.getByTestId("profile-sign-out"));
    expect(session.signOut).toHaveBeenCalledOnce();
  });
});
