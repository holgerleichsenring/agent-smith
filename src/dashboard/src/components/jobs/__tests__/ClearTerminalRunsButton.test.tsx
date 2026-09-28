import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import type { CallerIdentity } from "@/lib/identityApi";

let mockIdentity: CallerIdentity | null = null;
// The token only decides whether the identity is asked for; the identity mock answers it.
vi.mock("@/hooks/useAccessToken", () => ({ useAccessToken: () => null }));
vi.mock("@/hooks/useCallerIdentity", () => ({
  useCallerIdentity: () => ({ identity: mockIdentity, refusal: null, failure: null, loading: false }),
}));

import { ClearTerminalRunsButton } from "../ClearTerminalRunsButton";

function caller(permissions: string[]): CallerIdentity {
  return {
    authenticated: true, subject: "operator", issuer: null, roleClaim: "roles", groupClaim: "groups",
    roleClaimValues: [], groupClaimValues: [], roles: [], permissions, findings: [],
  };
}

describe("ClearTerminalRunsButton", () => {
  const originalFetch = globalThis.fetch;

  beforeEach(() => {
    mockIdentity = null;
    globalThis.fetch = vi.fn().mockResolvedValue({ ok: true, status: 200 } as Response);
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it("ClearTerminalRunsButton_Confirmed_DeletesTerminalOnly", async () => {
    render(<ClearTerminalRunsButton />);
    const button = screen.getByTestId("clear-terminal-runs");
    expect(button).toHaveTextContent("clear finished");

    fireEvent.click(button);
    expect(button).toHaveTextContent("finished runs and their verdict history go");
    expect(globalThis.fetch).not.toHaveBeenCalled();

    fireEvent.click(button);
    // 2026-08-25-2de1: the sign-in loop settles before the first request goes
    // out, so the call lands a tick after the click rather than inside it.
    await waitFor(() =>
      expect(globalThis.fetch).toHaveBeenCalledWith(
        "/api/runs?state=terminal",
        expect.objectContaining({ method: "DELETE" }),
      ),
    );
    await waitFor(() => expect(button).toHaveTextContent("clear finished"));
  });

  it("ClearTerminalRunsButton_CallerWithoutRunsDelete_IsNotOffered", () => {
    mockIdentity = caller(["runs.read"]);
    render(<ClearTerminalRunsButton />);
    expect(screen.queryByTestId("clear-terminal-runs")).toBeNull();
  });

  it("ClearTerminalRunsButton_CallerWithRunsDelete_IsOffered", () => {
    mockIdentity = caller(["runs.read", "runs.delete"]);
    render(<ClearTerminalRunsButton />);
    expect(screen.getByTestId("clear-terminal-runs")).toHaveTextContent("clear finished");
  });
});
