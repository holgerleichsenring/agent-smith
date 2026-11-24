import { render } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";

// 2026-11-23-933da: / sends a known PoC-only caller to /pocs; anyone else, and an unknown caller, stays.

const hoisted = vi.hoisted(() => ({ replace: vi.fn(), identity: null as CallerIdentity | null }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ replace: hoisted.replace }) }));
vi.mock("@/hooks/useKnownIdentity", () => ({ useKnownIdentity: () => hoisted.identity }));

import { PocOnlyRedirect } from "../PocOnlyRedirect";

function identity(permissions: string[], projectPermissions?: Record<string, string[]>): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: "i", roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [], projectPermissions,
  };
}

beforeEach(() => {
  hoisted.replace.mockReset();
  hoisted.identity = null;
});

describe("PocOnlyRedirect", () => {
  it("PocOnlyRedirect_PocOnlyCaller_GoesToPocs", () => {
    hoisted.identity = identity(["pocs.read"], { "poc-ann": ["runs.read"] });
    render(<PocOnlyRedirect />);
    expect(hoisted.replace).toHaveBeenCalledWith("/pocs");
  });

  it("PocOnlyRedirect_OperatorOrUnknown_StaysHome", () => {
    hoisted.identity = identity(["pocs.read", "runs.read"]);
    const view = render(<PocOnlyRedirect />);
    view.unmount();
    hoisted.identity = null;
    render(<PocOnlyRedirect />);
    expect(hoisted.replace).not.toHaveBeenCalled();
  });
});
