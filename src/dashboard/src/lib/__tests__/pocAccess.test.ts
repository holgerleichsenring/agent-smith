import { describe, expect, it } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";
import { isPocOnly } from "@/lib/pocAccess";

// 2026-11-23-933da: a PoC-only caller holds pocs.read, no global runs.read, and reads runs only in poc- projects.

function identity(permissions: string[], projectPermissions?: Record<string, string[]>): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: "i", roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [], projectPermissions,
  };
}

describe("isPocOnly", () => {
  it("isPocOnly_PocRoleBoundToTheirPocProject_IsPocOnly", () => {
    expect(isPocOnly(identity(["pocs.read", "pocs.write"], { "Poc-Ann": ["runs.read"] }))).toBe(true);
    expect(isPocOnly(identity(["pocs.read"]))).toBe(true);
  });

  it("isPocOnly_GlobalRunsReadOrAnotherProject_IsNot", () => {
    expect(isPocOnly(identity(["pocs.read", "runs.read"]))).toBe(false);
    expect(isPocOnly(identity(["pocs.read"], { "poc-ann": ["runs.read"], alpha: ["runs.read"] }))).toBe(false);
  });

  it("isPocOnly_NoPocsReadOrUnknown_IsNot", () => {
    expect(isPocOnly(identity([], { "poc-ann": ["runs.read"] }))).toBe(false);
    expect(isPocOnly(null)).toBe(false);
  });
});
