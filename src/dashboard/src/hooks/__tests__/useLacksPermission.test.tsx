import { renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-10-15-4a4de: "known to lack" asks the project the act names; an unknown caller is never told no.

const hoisted = vi.hoisted(() => ({ identity: null as CallerIdentity | null }));
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => "token" }));
vi.mock("@/hooks/useCallerIdentity", () => ({
  useCallerIdentity: () => ({ identity: hoisted.identity, refusal: null, failure: null, loading: false }),
}));

import { useLacksPermission } from "../useLacksPermission";

function caller(permissions: string[], projectPermissions?: Record<string, string[]>): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: null, roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [], projectPermissions,
  };
}

describe("useLacksPermission", () => {
  beforeEach(() => {
    hoisted.identity = null;
  });

  it("NoIdentity_NeverLacks", () => {
    expect(renderHook(() => useLacksPermission("runs.control", "alpha")).result.current).toBe(false);
  });

  it("ProjectGrantInAnotherProject_Lacks", () => {
    hoisted.identity = caller(["identity.read"], { beta: ["runs.control"] });
    expect(renderHook(() => useLacksPermission("runs.control", "alpha")).result.current).toBe(true);
    expect(renderHook(() => useLacksPermission("runs.control", "beta")).result.current).toBe(false);
    expect(renderHook(() => useLacksPermission("runs.control")).result.current).toBe(true);
  });
});
