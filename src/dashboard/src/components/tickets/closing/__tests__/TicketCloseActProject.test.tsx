import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";
import { ticket } from "../../__tests__/ticketFixtures";

// 2026-10-15-4a4de: the real useLacksPermission over a mocked identity — Close asks the ticket's own project.
// TicketClose.test.tsx mocks the hook file-wide, so these cases live here.

const hoisted = vi.hoisted(() => ({ identity: null as CallerIdentity | null }));
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => "token" }));
vi.mock("@/hooks/useCallerIdentity", () => ({
  useCallerIdentity: () => ({ identity: hoisted.identity, refusal: null, failure: null, loading: false }),
}));

const { TicketCloseAct } = await import("../TicketCloseAct");

function controlIn(project: string): CallerIdentity {
  return {
    authenticated: true, subject: "s", issuer: null, roleClaim: "roles", groupClaim: "groups", roleClaimValues: [],
    groupClaimValues: [], roles: [], permissions: ["identity.read"], findings: [], projectPermissions: { [project]: ["runs.read", "runs.control"] },
  };
}

beforeEach(() => {
  hoisted.identity = null;
});

describe("TicketCloseAct per project", () => {
  it("CloseAct_CallerHoldsRunsControlOnlyInTheTicketsProject_IsOffered", () => {
    hoisted.identity = controlIn("Shop");
    render(<TicketCloseAct ticket={ticket({ bucket: "filed" })} onClosed={() => {}} />);
    expect(screen.getByTestId("ticket-close-101")).toBeInTheDocument();
  });

  it("CloseAct_CallerHoldsRunsControlOnlyInAnotherProject_IsHidden", () => {
    hoisted.identity = controlIn("billing");
    render(<TicketCloseAct ticket={ticket({ bucket: "filed" })} onClosed={() => {}} />);
    expect(screen.queryByTestId("ticket-close-101")).toBeNull();
  });
});
