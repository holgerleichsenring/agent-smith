import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { IdentityView } from "../IdentityView";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import { ApiRefusal } from "@/lib/apiResponse";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-08-25-4530: the surface p0503d's endpoint exists for. The case it was
// built for is a caller with NO roles — the first login of an installation that
// has just configured an authority, where the only way to write a mapping is to
// read what the directory actually sent.
const server = vi.hoisted(() => ({ identity: vi.fn(), signIn: vi.fn() }));
vi.mock("@/lib/identityApi", () => ({ fetchIdentity: () => server.identity() }));
vi.mock("@/lib/auth/session", () => ({
  startAuthSession: () => Promise.resolve(null),
  signIn: server.signIn,
  signOut: vi.fn(),
}));

const identity = (over: Partial<CallerIdentity> = {}): CallerIdentity => ({
  authenticated: true,
  subject: "0a1b2c3d-4e5f",
  issuer: "https://login.example/realm",
  roleClaim: "roles",
  groupClaim: "groups",
  roleClaimValues: [],
  groupClaimValues: [],
  roles: [],
  permissions: ["identity.read"],
  findings: [],
  ...over,
});

function renderView(authority = "https://login.example/realm") {
  return render(
    <RuntimeSettingsProvider settings={{ auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority } }}>
      <IdentityView />
    </RuntimeSettingsProvider>,
  );
}

describe("IdentityView", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock
  // itself as the teardown hook, which then calls it after the test.
  beforeEach(() => {
    server.identity.mockReset();
    server.signIn.mockReset();
  });

  it("Identity_SignedInWithNoRoles_ShowsTheClaimAndTheValuesThatArrived", async () => {
    server.identity.mockResolvedValue(
      identity({ groupClaimValues: ["/platform-operators"], roleClaimValues: [] }),
    );

    renderView();

    // Which claim was looked in matters as much as what was in it: a claim that
    // arrived empty and a claim nobody read produce the same blank.
    expect(await screen.findByTestId("identity-role-claim")).toHaveTextContent("roles");
    expect(screen.getByTestId("identity-role-claim")).toHaveTextContent("nothing arrived");
    expect(screen.getByTestId("identity-group-claim")).toHaveTextContent("groups");
    expect(screen.getByTestId("identity-group-claim")).toHaveTextContent("/platform-operators");
    expect(screen.getByTestId("identity-no-roles")).toHaveTextContent("no role this installation maps");
  });

  it("Identity_SignedIn_ShowsTheResolvedRolesAndPermissions", async () => {
    server.identity.mockResolvedValue(
      identity({
        roleClaimValues: ["Operator"],
        roles: ["operator"],
        permissions: ["identity.read", "runs.read", "runs.write"],
      }),
    );

    renderView();

    expect(await screen.findByTestId("identity-roles")).toHaveTextContent("operator");
    const permissions = screen.getByTestId("identity-permissions");
    expect(permissions).toHaveTextContent("runs.read");
    expect(permissions).toHaveTextContent("runs.write");
    expect(screen.queryByTestId("identity-no-roles")).toBeNull();
  });

  it("Identity_TheServerRefusedTheRead_OffersASignIn", async () => {
    server.identity.mockRejectedValue(new ApiRefusal("/api/identity", 401, "sign-in", []));

    renderView();

    expect(await screen.findByTestId("refusal-surface")).toHaveTextContent("You are signed out");
  });

  it("Identity_NoAuthorityConfigured_SaysNothingSignsIn", async () => {
    server.identity.mockResolvedValue(identity());

    renderView("");

    expect(await screen.findByTestId("identity-unconfigured")).toHaveTextContent("anonymous");
    expect(server.identity).not.toHaveBeenCalled();
  });
});
