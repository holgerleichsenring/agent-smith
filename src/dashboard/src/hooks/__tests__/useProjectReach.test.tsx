import { act, renderHook } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-10-15-4a4de: 'none' only for a signed-in, non-global reader on an enforcing server whose readable project
// list came back empty; every other answer is 'unknown', and a global reader causes no request at all.

const hoisted = vi.hoisted(() => ({
  identity: null as CallerIdentity | null,
  requirements: vi.fn(),
  projects: vi.fn(),
}));
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => "token" }));
vi.mock("@/hooks/useCallerIdentity", () => ({
  useCallerIdentity: () => ({ identity: hoisted.identity, refusal: null, failure: null, loading: false }),
}));
vi.mock("@/lib/authRequirementsApi", () => ({ fetchAuthRequirements: () => hoisted.requirements() }));
vi.mock("@/lib/readableProjectsApi", () => ({ fetchReadableProjects: () => hoisted.projects() }));

import { useProjectReach } from "../useProjectReach";

function caller(permissions: string[], projectPermissions?: Record<string, string[]>): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: null, roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [], projectPermissions,
  };
}

async function reach() {
  const hook = renderHook(() => useProjectReach());
  await act(async () => {});
  return hook.result.current;
}

describe("useProjectReach", () => {
  beforeEach(() => {
    hoisted.identity = null;
    hoisted.requirements.mockReset();
    hoisted.requirements.mockResolvedValue({ enforced: true });
    hoisted.projects.mockReset();
    hoisted.projects.mockResolvedValue([]);
  });

  it("GlobalRunsReadHolder_MakesNoRequest", async () => {
    hoisted.identity = caller(["identity.read", "runs.read"]);
    expect(await reach()).toBe("unknown");
    expect(hoisted.requirements).not.toHaveBeenCalled();
    expect(hoisted.projects).not.toHaveBeenCalled();
  });

  it("ProjectOnlyCaller_EmptyProjectList_IsNone", async () => {
    hoisted.identity = caller(["identity.read"], { retired: ["runs.read"] });
    expect(await reach()).toBe("none");
    expect(hoisted.projects).toHaveBeenCalledTimes(1);
  });

  it("ProjectOnlyCaller_AProjectListed_IsUnknown", async () => {
    hoisted.identity = caller(["identity.read"], { alpha: ["runs.read"] });
    hoisted.projects.mockResolvedValue(["alpha"]);
    expect(await reach()).toBe("unknown");
  });

  it("ProjectsReadRefused_IsUnknownNotNone", async () => {
    hoisted.identity = caller(["identity.read"]);
    hoisted.projects.mockRejectedValue(new Error("403"));
    expect(await reach()).toBe("unknown");
  });

  it("NotEnforced_IsUnknown_ProjectsNotRead", async () => {
    hoisted.identity = caller(["identity.read"]);
    hoisted.requirements.mockResolvedValue({ enforced: false });
    expect(await reach()).toBe("unknown");
    expect(hoisted.projects).not.toHaveBeenCalled();
  });

  it("NoIdentity_IsUnknown_NothingRead", async () => {
    expect(await reach()).toBe("unknown");
    expect(hoisted.requirements).not.toHaveBeenCalled();
  });
});
