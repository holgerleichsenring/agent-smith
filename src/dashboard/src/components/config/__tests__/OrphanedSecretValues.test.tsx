import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

const api = vi.hoisted(() => ({
  fetchSecretValues: vi.fn(),
  deleteSecretValue: vi.fn(),
}));
vi.mock("@/lib/secretValuesApi", () => api);

import { OrphanedSecretValues } from "../OrphanedSecretValues";

const orphan = { name: "old_token", setBy: "ada", setAt: "2026-11-17T10:00:00Z", keyId: "env:1", keyCurrent: true, readable: true };
const view = (orphans: unknown[]) => ({ masterKeyPresent: true, masterKeyProblem: null, masterKeySource: null, secrets: [], orphans });

afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("OrphanedSecretValues", () => {
  it("lists a stored value whose secret is gone and deletes it through the secret route", async () => {
    api.fetchSecretValues.mockResolvedValueOnce(view([orphan])).mockResolvedValueOnce(view([]));
    api.deleteSecretValue.mockResolvedValue({ line: "The stored value of 'old_token' was deleted in run r-1." });
    render(<OrphanedSecretValues />);

    await waitFor(() => expect(screen.getByTestId("secret-orphan-old_token").textContent).toContain("set by ada"));
    fireEvent.click(screen.getByTestId("secret-orphan-delete-old_token"));

    await waitFor(() => expect(screen.getByTestId("secret-orphans-line").textContent).toContain("deleted in run r-1"));
    expect(api.deleteSecretValue).toHaveBeenCalledWith("old_token");
    expect(api.fetchSecretValues).toHaveBeenCalledTimes(2);
    expect(screen.queryByTestId("secret-orphan-old_token")).toBeNull();
  });

  it("renders nothing when no value is orphaned", async () => {
    api.fetchSecretValues.mockResolvedValue(view([]));
    render(<OrphanedSecretValues />);

    await waitFor(() => expect(api.fetchSecretValues).toHaveBeenCalled());
    expect(screen.queryByTestId("secret-orphans")).toBeNull();
  });
});
