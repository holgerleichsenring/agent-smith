import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { IdentityView } from "../IdentityView";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import { DEFAULT_RUNTIME_SETTINGS } from "@/lib/runtimeSettings/runtimeSettings";
import { ApiRefusal } from "@/lib/apiResponse";
import type { CallerIdentity } from "@/lib/identityApi";
import type { AuthRequirements } from "@/lib/authRequirementsApi";

// 2026-08-25-4530: the surface p0503d's endpoint exists for. The case it was
// built for is a caller with NO roles — the first login of an installation that
// has just configured an authority, where the only way to write a mapping is to
// read what the directory actually sent.
const server = vi.hoisted(() => ({
  identity: vi.fn(),
  signIn: vi.fn(),
  requirements: vi.fn(),
}));
vi.mock("@/lib/identityApi", () => ({ fetchIdentity: () => server.identity() }));
// 2026-08-25-1806: the anonymous route now also says whether THIS caller's token was
// refused — the one answer an enforcing server still gives the caller it rejected.
vi.mock("@/lib/authRequirementsApi", () => ({
  fetchAuthRequirements: () => server.requirements(),
}));
vi.mock("@/lib/auth/session", () => ({
  startAuthSession: () => Promise.resolve(null),
  signIn: server.signIn,
  signOut: vi.fn(),
}));
// 2026-09-14-d4e8: whether this tab HOLDS a token is what decides between an inference and
// the proof that outranks it, so it is stated per test rather than left to a real store.
const tab = vi.hoisted(() => ({ token: vi.fn<() => string | null>(() => null) }));
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => tab.token() }));

const requirements = (over: Partial<AuthRequirements> = {}): AuthRequirements => ({
  enforced: true,
  authority: "https://login.example/realm",
  audience: "agent-smith",
  tokenRefusal: null,
  presentedAudience: null,
  presentedIssuer: null,
  presentedTokenVersion: null,
  ...over,
});

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

function renderView(
  authority = "https://login.example/realm",
  over: Partial<typeof DEFAULT_RUNTIME_SETTINGS.auth> = {},
) {
  return render(
    <RuntimeSettingsProvider
      settings={{ auth: { ...DEFAULT_RUNTIME_SETTINGS.auth, authority, ...over } }}
    >
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
    server.requirements.mockReset();
    // Signed out is the state every case before 2026-09-14-d4e8 was written in.
    tab.token.mockReset();
    tab.token.mockReturnValue(null);
    server.requirements.mockResolvedValue(requirements());
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

  it("Identity_TokenRefused_SaysSoRatherThanShowingAnEmptyMapping", async () => {
    // The state the page used to render as "nothing arrived" against every field, which
    // sends an operator to write a mapping when the audience is what is wrong.
    server.requirements.mockResolvedValue(requirements({ tokenRefusal: "audience" }));
    server.identity.mockRejectedValue(new ApiRefusal("/api/identity", 401, "sign-in", []));

    renderView();

    const refused = await screen.findByTestId("identity-token-refused");
    await waitFor(() => expect(refused).toHaveTextContent("not issued for the audience"));
    expect(refused).toHaveAttribute("data-refusal", "audience");
    expect(refused).toHaveTextContent("agent-smith");
    expect(refused).toHaveTextContent("No role mapping can change this");
    expect(screen.queryByTestId("identity-facts")).toBeNull();
    expect(screen.queryByTestId("identity-no-roles")).toBeNull();
  });

  it("Identity_TokenAcceptedWithNoRole_StillShowsWhatArrived", async () => {
    // The control for the case above: nothing was refused, so this IS a mapping to write.
    server.requirements.mockResolvedValue(requirements({ tokenRefusal: null }));
    server.identity.mockResolvedValue(identity({ groupClaimValues: ["/platform-operators"] }));

    renderView();

    expect(await screen.findByTestId("identity-facts")).toBeInTheDocument();
    expect(screen.getByTestId("identity-no-roles")).toBeInTheDocument();
    expect(screen.queryByTestId("identity-token-refused")).toBeNull();
  });

  it("Identity_TokenExpired_NamesTheExpiryRatherThanTheAudience", async () => {
    server.requirements.mockResolvedValue(requirements({ tokenRefusal: "expired" }));
    server.identity.mockResolvedValue(identity({ authenticated: false }));

    renderView();

    const refused = await screen.findByTestId("identity-token-refused");
    await waitFor(() => expect(refused).toHaveTextContent("has expired"));
  });

  // 2026-09-14-c72e: naming the check that failed without naming the value that failed it
  // is half an answer, and the missing half was a session of decoding tokens by hand.
  it("Identity_TokenRefused_ShowsExpectedBesidePresented", async () => {
    server.requirements.mockResolvedValue(
      requirements({
        tokenRefusal: "audience",
        audience: "agent-smith",
        presentedAudience: "api://agent-smith",
        presentedIssuer: "https://sts.example/tenant/",
        presentedTokenVersion: "1.0",
      }),
    );
    server.identity.mockResolvedValue(identity({ authenticated: false }));

    renderView();

    const refused = await screen.findByTestId("identity-token-refused");
    await waitFor(() =>
      expect(screen.getByTestId("presented-audience")).toHaveTextContent("api://agent-smith"),
    );
    expect(refused).toHaveTextContent("agent-smith");
    expect(screen.getByTestId("presented-issuer")).toHaveTextContent("https://sts.example/tenant/");
    expect(screen.getByTestId("presented-version")).toHaveTextContent("1.0");
  });

  it("Identity_TokenRefusedAndNothingCouldBeDecoded_ShowsTheExpectedHalfAlone", async () => {
    server.requirements.mockResolvedValue(requirements({ tokenRefusal: "malformed" }));
    server.identity.mockResolvedValue(identity({ authenticated: false }));

    renderView();

    await screen.findByTestId("identity-token-refused");
    expect(screen.getByTestId("presented-audience")).toHaveTextContent("not readable");
    expect(screen.queryByTestId("presented-version")).toBeNull();
  });

  // 2026-09-14-d4e8: the one thing a refusal cannot say, because it only says it afterwards.
  const MISMATCHED = { scopes: "openid api://some-other-api/WebApp" };

  it("Identity_ScopeMismatchAndNoTokenHeld_NamesBothResources", async () => {
    server.requirements.mockResolvedValue(requirements({ audience: "agent-smith" }));
    server.identity.mockResolvedValue(identity({ authenticated: false }));

    renderView("https://login.example/realm", MISMATCHED);

    const remark = await screen.findByTestId("identity-scope-remark");
    expect(remark).toHaveTextContent("api://some-other-api");
    expect(remark).toHaveTextContent("agent-smith");
  });

  it("Identity_ScopeMismatchButATokenWasAccepted_SaysNothing", async () => {
    // A token in hand that was not refused has proven the scopes produce something this
    // server takes, whatever the strings look like. Proof outranks the inference.
    tab.token.mockReturnValue("a-token-this-server-took");
    server.requirements.mockResolvedValue(requirements({ audience: "agent-smith" }));
    server.identity.mockResolvedValue(identity());

    renderView("https://login.example/realm", MISMATCHED);

    expect(await screen.findByTestId("identity-facts")).toBeInTheDocument();
    expect(screen.queryByTestId("identity-scope-remark")).toBeNull();
  });

  it("Identity_TokenWasRefused_TheComparisonSpeaksAlone", async () => {
    // The refusal already shows both sides and names the shape; a second surface arguing
    // beside it on the same page is noise.
    server.requirements.mockResolvedValue(
      requirements({ audience: "agent-smith", tokenRefusal: "audience" }),
    );
    server.identity.mockResolvedValue(identity({ authenticated: false }));

    renderView("https://login.example/realm", MISMATCHED);

    expect(await screen.findByTestId("identity-token-refused")).toBeInTheDocument();
    expect(screen.queryByTestId("identity-scope-remark")).toBeNull();
  });

  it("Identity_NoAuthorityConfigured_SaysNothingSignsIn", async () => {
    server.identity.mockResolvedValue(identity());

    renderView("");

    expect(await screen.findByTestId("identity-unconfigured")).toHaveTextContent("anonymous");
    expect(server.identity).not.toHaveBeenCalled();
  });
});
