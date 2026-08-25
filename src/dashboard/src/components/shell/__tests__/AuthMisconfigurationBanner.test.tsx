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

const requirements = (over: Partial<AuthRequirements> = {}): AuthRequirements => ({
  enforced: false,
  authority: null,
  audience: null,
  tokenRefusal: null,
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

  it("Banner_TheTwoAuthoritiesDiffer_NamesBoth", async () => {
    server.requirements.mockResolvedValue(
      requirements({ enforced: true, authority: "https://login.example/realm-a" }),
    );

    renderBanner("https://login.example/realm-b");

    const banner = await screen.findByTestId("auth-misconfiguration-banner");
    expect(banner).toHaveAttribute("data-half", "both");
    expect(banner).toHaveTextContent("realm-a");
    expect(banner).toHaveTextContent("realm-b");
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
