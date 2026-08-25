import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ConnectionsView } from "../ConnectionsView";
import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";
import * as api from "@/lib/diagnosticsApi";

// 2026-08-25-3277: the conversion pinned on a representative converted loader.
// ConnectionsView stands for every surface that used to store `err.message`:
// what is asserted here is that the loader kept the VALUE it caught, which is
// the only reason the sign-in button can exist at all — and that an ordinary
// fault still reads exactly as it did before.

vi.mock("@/lib/diagnosticsApi", () => ({
  fetchConnections: vi.fn(),
  probeConnection: vi.fn(),
}));

const session = vi.hoisted(() => ({ signIn: vi.fn() }));
vi.mock("@/lib/auth/session", () => ({ signIn: session.signIn }));

const mockedApi = api as unknown as {
  fetchConnections: ReturnType<typeof vi.fn>;
  probeConnection: ReturnType<typeof vi.fn>;
};

describe("a loader keeps the refusal it was given", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock
  // itself as the teardown hook, which then calls it after the test.
  beforeEach(() => {
    mockedApi.fetchConnections.mockReset();
    mockedApi.probeConnection.mockReset();
    session.signIn.mockReset();
  });

  it("Loader_A401_HoldsTheRefusalRatherThanItsMessage", async () => {
    mockedApi.fetchConnections.mockRejectedValue(
      new ApiRefusal("/api/diagnostics/connections", 401, "sign-in", []),
    );

    render(<ConnectionsView />);

    // The refusal reached the surface as a refusal — not as the sentence a
    // message-holding loader would have left behind.
    await waitFor(() =>
      expect(screen.getByTestId("refusal-surface")).toHaveAttribute("data-refusal", "sign-in"),
    );
    expect(screen.queryByTestId("connections-error")).toBeNull();
  });

  it("Loader_A401_RendersTheSignInAction", async () => {
    mockedApi.fetchConnections.mockRejectedValue(
      new ApiRefusal("/api/diagnostics/connections", 401, "sign-in", []),
    );

    render(<ConnectionsView />);
    fireEvent.click(await screen.findByTestId("refusal-sign-in"));

    expect(session.signIn).toHaveBeenCalledOnce();
  });

  it("Loader_A403_NamesThePermissionAndOffersNoSignIn", async () => {
    mockedApi.fetchConnections.mockRejectedValue(
      new ApiRefusal("/api/diagnostics/connections", 403, "permission", ["diagnostics.read"]),
    );

    render(<ConnectionsView />);

    await waitFor(() =>
      expect(screen.getByTestId("refusal-missing-permissions")).toHaveTextContent(
        "diagnostics.read",
      ),
    );
    // Signing in again returns the same token carrying the same roles.
    expect(screen.queryByTestId("refusal-sign-in")).toBeNull();
  });

  it("Loader_AServerError_StillRendersTheMessageItDoesToday", async () => {
    mockedApi.fetchConnections.mockRejectedValue(
      new ApiResponseError("/api/diagnostics/connections", 500, "HTTP 500"),
    );

    render(<ConnectionsView />);

    await waitFor(() =>
      expect(screen.getByTestId("connections-error")).toHaveTextContent(
        "Failed to load connections: /api/diagnostics/connections: HTTP 500",
      ),
    );
    expect(screen.queryByTestId("refusal-surface")).toBeNull();
  });
});
