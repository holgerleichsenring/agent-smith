import { describe, expect, it } from "vitest";
import { scopeMismatch } from "../scopeAddresses";

// 2026-09-14-d4e8: the rule may only ever say "your scope names X, this server validates Y".
// Every other shape is silence, and most of these cases are silences — which is the point:
// a rule that accuses a correct installation is worse than no rule.

const API = "047bfc13-the-api";
const NO_AUDIENCE_HERE = "";

describe("scopeMismatch", () => {
  it("Scope_NamesAnotherResource_IsReported", () => {
    const mismatch = scopeMismatch(
      `openid profile api://some-other-api/WebApp`, NO_AUDIENCE_HERE, API);

    expect(mismatch).toEqual({ asked: "api://some-other-api", validated: API });
  });

  it("Scope_NamesTheServersResourceAsABareId_IsSilent", () => {
    // A version 2 access token's audience is the bare client id; the scope still carries
    // the identifier URI. Same resource.
    expect(scopeMismatch(`api://${API}/WebApp`, NO_AUDIENCE_HERE, API)).toBeNull();
  });

  it("Scope_NamesTheServersResourceAsAnAppIdUri_IsSilent", () => {
    expect(scopeMismatch(`api://${API}/WebApp`, NO_AUDIENCE_HERE, `api://${API}`)).toBeNull();
  });

  it("Scope_NoApiScopeConfiguredAtAll_IsSilent", () => {
    // This repository's own example: a Keycloak realm, audience 'agent-smith', scopes
    // 'openid profile'. Reporting it would accuse a shipped, correct configuration.
    expect(scopeMismatch("openid profile", NO_AUDIENCE_HERE, "agent-smith")).toBeNull();
  });

  it("Scope_TheServerExpectsNoAudience_IsSilent", () => {
    // A server validating no audience accepts a token minted for anything.
    expect(scopeMismatch(`api://${API}/WebApp`, NO_AUDIENCE_HERE, null)).toBeNull();
    expect(scopeMismatch(`api://${API}/WebApp`, NO_AUDIENCE_HERE, "   ")).toBeNull();
  });

  it("Scope_AnAudienceIsConfiguredOnTheDashboardSide_IsSilent", () => {
    // The authority partitions tokens by audience and the client sends it as a request
    // parameter, so the scopes are plain permission names and this rule reads the wrong half.
    expect(scopeMismatch("read:runs write:runs", "https://api.example/agent", API)).toBeNull();
  });

  it("Scope_TheAudienceCarriesATrailingSlash_IsSilent", () => {
    expect(scopeMismatch(`api://${API}/WebApp`, NO_AUDIENCE_HERE, `api://${API}/`)).toBeNull();
  });

  it("Scope_TheResourceIsWrittenInAnotherCase_IsSilent", () => {
    expect(scopeMismatch(`api://${API.toUpperCase()}/WebApp`, NO_AUDIENCE_HERE, API)).toBeNull();
  });

  it("Scope_AGraphScopeBesideTheApiScope_IsSilent", () => {
    expect(scopeMismatch(
      `openid profile https://graph.microsoft.com/User.Read api://${API}/WebApp`,
      NO_AUDIENCE_HERE, API)).toBeNull();
  });

  it("Scope_AnApiUriWithNoScopeName_NamesNoResource", () => {
    // "api://x" alone is a resource, not a scope; recovering a resource from it would mean
    // dropping the id itself.
    expect(scopeMismatch(`api://${API}`, NO_AUDIENCE_HERE, "some-other-api")).toBeNull();
  });
});
