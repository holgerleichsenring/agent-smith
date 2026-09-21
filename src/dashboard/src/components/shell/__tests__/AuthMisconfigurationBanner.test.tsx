import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthMisconfigurationBanner } from "../AuthMisconfigurationBanner";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import type { AuthRequirements } from "@/lib/authRequirementsApi";

// 2026-08-25-4530: the dashboard is the only place that holds BOTH halves — the
// server never learns what the browser was given, and the browser never learns
// what the server demands.
const server = vi.hoisted(() => ({ requirements: vi.fn() }));
vi.mock("@/lib/authRequirementsApi", () => ({
  fetchAuthRequirements: () => server.requirements(),
}));

// 2026-09-14-c72e: whether this tab HOLDS a token is the half the browser contributes —
// the server's null refusal cannot tell an accepted token from a request that carried
// none. Mocked at the hook, because what is under test is what the banner concludes.
const tab = vi.hoisted(() => ({ token: vi.fn<() => string | null>() }));
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => tab.token() }));

const requirements = (over: Partial<AuthRequirements> = {}): AuthRequirements => ({
  enforced: false,
  authority: null,
  audience: null,
  tokenRefusal: null,
  presentedAudience: null,
  presentedIssuer: null,
  presentedTokenVersion: null,
  ...over,
});

function renderBanner(dashboardAuthority: string) {
  return render(
    <RuntimeSettingsProvider
      settings={{
        auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority: dashboardAuthority },
      }}
    >
      <AuthMisconfigurationBanner />
    </RuntimeSettingsProvider>,
  );
}

/** The banner renders after the requirements land, so every case waits. */
async function bannerOrNothing(): Promise<HTMLElement | null> {
  await waitFor(() => expect(server.requirements).toHaveBeenCalled());
  return screen.queryByTestId("auth-misconfiguration-banner");
}

describe("AuthMisconfigurationBanner", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock
  // itself as the teardown hook, which then calls it after the test.
  beforeEach(() => {
    server.requirements.mockReset();
    tab.token.mockReset();
    // Signed out is the state every pre-2026-09-14-c72e case was written in.
    tab.token.mockReturnValue(null);
  });

  it("Banner_ServerEnforcesAndDashboardHasNoAuthority_NamesTheDashboardHalf", async () => {
    // The failure mode worth building for: every call answers 401 and every
    // route renders nothing, with no clue anywhere.
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm" }),
    );

    renderBanner("");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "dashboard");
    expect(banner).toHaveTextContent("this dashboard has no authority configured");
    expect(banner).toHaveTextContent("every call it makes is refused");
  });

  it("Banner_DashboardHasAnAuthorityAndServerHasNone_NamesTheServerHalf", async () => {
    server.requirements.mockResolvedValue(requirements());

    renderBanner("https://login.example/realm");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "server");
    expect(banner).toHaveTextContent("the server has no authority configured");
  });

  // 2026-09-14-c72e: Banner_TheTwoAuthoritiesDiffer_NamesBoth was this pair's ancestor. It
  // asserted the banner from the strings alone, which is the reading this phase replaced —
  // so it is split by what the tokens prove rather than deleted.
  it("Banner_AuthoritiesDifferAndNoTokenHeld_StillNamesTheConflict", async () => {
    // Nothing has been tried yet, so nothing has been proven. Going quiet here would take
    // the banner away from the one person it was built for: somebody who cannot sign in.
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm-a" }),
    );

    renderBanner("https://login.example/realm-b");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "both");
    expect(banner).toHaveTextContent("realm-a");
    expect(banner).toHaveTextContent("realm-b");
  });

  it("Banner_AuthoritiesDifferAndTokenHeldAndAccepted_SaysNothing", async () => {
    // The measured case: a v2 sign-in minting an id_token beside a v1 resource validating
    // an access token. Two strings, one directory, and a token that just went through.
    tab.token.mockReturnValue("a-token-this-server-took");
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm-a" }),
    );

    renderBanner("https://login.example/realm-b");

    expect(await bannerOrNothing()).toBeNull();
  });

  it("Banner_AuthoritiesDifferAndAudienceRefused_NamesTheConflict", async () => {
    tab.token.mockReturnValue("a-token-this-server-refused");
    server.requirements.mockResolvedValue(
      requirements({
        enforced: true,
        authority: "https://login.example/realm-a",
        tokenRefusal: "audience",
      }),
    );

    renderBanner("https://login.example/realm-b");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "both");
  });

  it("Banner_AuthoritiesDifferAndTokenExpired_SaysNothing", async () => {
    // An expiry says nothing about either authority, and neither does a server that cannot
    // reach its own. Accusing the configuration for those is the false positive this
    // banner exists to remove, arriving from the other side.
    tab.token.mockReturnValue("a-token-that-ran-out");
    server.requirements.mockResolvedValue(
      requirements({
        enforced: true,
        authority: "https://login.example/realm-a",
        tokenRefusal: "expired",
      }),
    );

    renderBanner("https://login.example/realm-b");

    expect(await bannerOrNothing()).toBeNull();
  });

  // 2026-09-21-291b: the measured false positive. One directory publishes two
  // endpoints and they write two strings; recogniseShape names the bare authority
  // beside an api:// audience as a way OUT of a fault, so the banner was accusing
  // the fixed state — on every load of an enforcing installation, where no token is
  // ever held before the first sign-in.
  it("Banner_AuthoritiesDifferOnlyByTheVersionSuffixAndNoTokenHeld_SaysNothing", async () => {
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/tenant/" }),
    );

    renderBanner("https://login.example/tenant/v2.0");

    expect(await bannerOrNothing()).toBeNull();
  });

  it("Banner_VersionSuffixPairAndIssuerRefused_NamesTheConflict", async () => {
    // The suffix answers only "is a difference we have no token to test a reason to
    // accuse". A token that WAS minted and refused on its issuer settles it, and the
    // sentence must still print the operator's own two strings.
    tab.token.mockReturnValue("a-token-this-server-refused");
    server.requirements.mockResolvedValue(
      requirements({
        enforced: true,
        authority: "https://login.example/tenant",
        tokenRefusal: "issuer",
      }),
    );

    renderBanner("https://login.example/tenant/v2.0");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "both");
    expect(banner).toHaveTextContent("https://login.example/tenant/v2.0");
  });

  it("Banner_AuthoritiesDifferByAnotherVersionLikeSegment_StillNamesTheConflict", async () => {
    // The suffix is LITERAL for this reason: a realm at /realms/v2 is a different
    // realm from /realms, and a version-shaped pattern would read a real two-realm
    // mistake as one directory on two endpoints.
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://kc.example/realms" }),
    );

    renderBanner("https://kc.example/realms/v2");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "both");
  });

  it("Banner_BothConfiguredAndDisagreeing_DoesNotSayOneSideOnly", async () => {
    // The heading stood above all three cases, and in this one both sides ARE
    // configured — it was the most-rendered sentence in the component and untrue.
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm-a" }),
    );

    renderBanner("https://login.example/realm-b");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveTextContent("name different authorities");
    expect(banner).not.toHaveTextContent("configured on one side only");
  });

  it("Banner_OneHalfUnconfigured_KeepsItsHeading", async () => {
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm" }),
    );

    renderBanner("");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveTextContent("Sign-in is configured on one side only.");
  });

  it("Banner_BothHalvesAgree_ShowsNothing", async () => {
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm/" }),
    );

    // The same issuer written two ways — one copied out of a discovery document,
    // one typed by hand — is one authority, not a misconfiguration.
    renderBanner("https://login.example/realm");

    expect(await bannerOrNothing()).toBeNull();
  });

  it("Banner_NothingConfiguredAnywhere_ShowsNothing", async () => {
    // Which is every installation today, and it stays silent.
    server.requirements.mockResolvedValue(requirements());

    renderBanner("");

    expect(await bannerOrNothing()).toBeNull();
  });

  it("Banner_TheServerDidNotAnswer_ShowsNothing", async () => {
    server.requirements.mockRejectedValue(new Error("network"));

    renderBanner("https://login.example/realm");

    expect(await bannerOrNothing()).toBeNull();
  });
});
