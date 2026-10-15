import { describe, expect, it } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";
import { holds, holdsInAnyProject } from "@/lib/projectPermissions";

// 2026-10-15-4a4de: effective rights in a project are the global permissions plus that project's binding grants.

function identity(permissions: string[], projectPermissions?: Record<string, string[]>): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: "i", roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [], projectPermissions,
  };
}

describe("holds", () => {
  it("holds_GlobalPermission_HoldsInEveryProject", () => {
    expect(holds(identity(["runs.control"]), "runs.control", "alpha")).toBe(true);
    expect(holds(identity(["runs.control"]), "runs.control", "beta")).toBe(true);
  });

  it("holds_ProjectGrant_HoldsInThatProjectOnly", () => {
    const caller = identity(["identity.read"], { alpha: ["runs.control"] });
    expect(holds(caller, "runs.control", "alpha")).toBe(true);
    expect(holds(caller, "runs.control", "beta")).toBe(false);
  });

  it("holds_NoProjectGiven_IgnoresProjectGrants", () => {
    expect(holds(identity([], { alpha: ["runs.control"] }), "runs.control")).toBe(false);
  });

  it("holds_ProjectNameDiffersInCase_Holds", () => {
    expect(holds(identity([], { Alpha: ["runs.control"] }), "runs.control", "alpha")).toBe(true);
  });

  it("holds_IdentityWithoutProjectPermissions_IsTheGlobalRule", () => {
    expect(holds(identity(["runs.read"]), "runs.read", "alpha")).toBe(true);
    expect(holds(identity(["runs.read"]), "runs.control", "alpha")).toBe(false);
  });
});

describe("holdsInAnyProject", () => {
  it("holdsInAnyProject_OneProjectGrant_Holds", () => {
    expect(holdsInAnyProject(identity([], { alpha: [], beta: ["runs.delete"] }), "runs.delete")).toBe(true);
    expect(holdsInAnyProject(identity([], { alpha: ["runs.read"] }), "runs.delete")).toBe(false);
    expect(holdsInAnyProject(identity(["runs.delete"]), "runs.delete")).toBe(true);
  });
});
