import { describe, expect, it } from "vitest";
import { recogniseShape } from "../recogniseShape";
import type { AuthRequirements } from "@/lib/authRequirementsApi";

// 2026-09-14-c72e: the shortcut, never the diagnosis. A shape nobody here recognises still
// leaves the comparison standing, so these cases are allowed to be a closed and short list.

const SIGN_IN_CLIENT = "bc3fea88-the-dashboard";
const API = "047bfc13-the-api";

const refused = (over: Partial<AuthRequirements> = {}): AuthRequirements => ({
  enforced: true,
  authority: "https://login.example/tenant/v2.0",
  audience: API,
  tokenRefusal: "audience",
  presentedAudience: null,
  presentedIssuer: null,
  presentedTokenVersion: null,
  ...over,
});

describe("recogniseShape", () => {
  it("Recognition_V1AudienceAndIssuer_NamesTheResourceTokenVersionAndBothWaysOut", () => {
    const recognition = recogniseShape(
      refused({ presentedAudience: `api://${API}`, presentedTokenVersion: "1.0" }),
      SIGN_IN_CLIENT,
    );

    expect(recognition).not.toBeNull();
    expect(recognition!.shape).toContain("version 1 access token");
    // The property is the whole point: it is what decides the version, and nothing in the
    // sign-in configuration does.
    expect(recognition!.shape).toContain("requestedAccessTokenVersion");
    expect(recognition!.waysOut).toHaveLength(2);
    expect(recognition!.waysOut[0]).toContain(API);
    expect(recognition!.waysOut[1]).toContain(`api://${API}`);
  });

  it("Recognition_V2AudienceAgainstAV1Server_NamesTheMirrorCase", () => {
    const recognition = recogniseShape(
      refused({
        audience: `api://${API}`,
        presentedAudience: API,
        presentedTokenVersion: "2.0",
      }),
      SIGN_IN_CLIENT,
    );

    expect(recognition!.shape).toContain("version 2 access token");
    expect(recognition!.waysOut[0]).toContain(API);
  });

  it("Recognition_AudienceIsTheSignInClient_NamesTheMissingApiScope", () => {
    const recognition = recogniseShape(
      refused({ presentedAudience: SIGN_IN_CLIENT }),
      SIGN_IN_CLIENT,
    );

    expect(recognition!.shape).toContain("issued for the sign-in client");
    expect(recognition!.waysOut[0]).toContain("scope");
  });

  it("Recognition_UnknownShape_TheComparisonStandsWithoutASentence", () => {
    const recognition = recogniseShape(
      refused({ presentedAudience: "some-entirely-other-service" }),
      SIGN_IN_CLIENT,
    );

    expect(recognition).toBeNull();
  });

  it("Recognition_NothingCouldBeDecoded_IsNotGuessedAt", () => {
    expect(recogniseShape(refused(), SIGN_IN_CLIENT)).toBeNull();
  });

  it("Recognition_NoSignInClientConfigured_DoesNotMatchAnEmptyAudience", () => {
    // An installation with no client id must not have every token read as "issued for the
    // sign-in client" because two empty strings are equal.
    expect(recogniseShape(refused({ presentedAudience: "" }), "")).toBeNull();
  });

  it("Recognition_AppIdUriWithAScopeAppended_IsNotTheSameResource", () => {
    // api://{id}/WebApp is a configuration error of its own — an audience carrying a scope
    // name — and reading it as the identifier-URI form would name the wrong remedy.
    const recognition = recogniseShape(
      refused({ presentedAudience: `api://${API}/WebApp`, presentedTokenVersion: "1.0" }),
      SIGN_IN_CLIENT,
    );

    expect(recognition).toBeNull();
  });
});
