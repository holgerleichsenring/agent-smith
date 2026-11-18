import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

const api = vi.hoisted(() => ({
  fetchSecretValues: vi.fn(),
  setSecretValue: vi.fn(),
  deleteSecretValue: vi.fn(),
  rewrapSecretValues: vi.fn(),
}));
vi.mock("@/lib/secretValuesApi", () => api);

import { SecretValueField } from "../SecretValueField";

const view = {
  masterKeyPresent: true, masterKeyProblem: null, orphans: [],
  masterKeySource: "Azure Key Vault key https://kv.vault.azure.net/keys/k/1 (AGENTSMITH_SECRET_MASTER_KEY_WRAPPED)",
  secrets: [{ name: "gh_token", variable: "GH_TOKEN", fromEnvironment: true, stored: { name: "gh_token", setBy: "ada", setAt: "2026-11-17T10:00:00Z", keyId: "env:1", keyCurrent: true, readable: true } }],
};

afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("SecretValueField", () => {
  it("says who set the value and that the environment wins, and never shows a value", async () => {
    api.fetchSecretValues.mockResolvedValue(view);
    render(<SecretValueField name="gh_token" />);

    await waitFor(() => expect(screen.getByTestId("secret-value-state").textContent).toContain("set by ada"));
    expect(screen.getByTestId("secret-value-state").textContent).toContain("GH_TOKEN is set and wins");
    expect((screen.getByTestId("secret-value-input") as HTMLInputElement).type).toBe("password");
    expect(screen.getByTestId("secret-value-key-source").textContent).toContain("Azure Key Vault key");
  });

  it("posts the typed value and clears the field whatever the answer", async () => {
    api.fetchSecretValues.mockResolvedValue(view);
    api.setSecretValue.mockResolvedValue({ line: "The value of 'gh_token' was stored encrypted." });
    render(<SecretValueField name="gh_token" />);
    await waitFor(() => screen.getByText(/set by ada/));

    fireEvent.change(screen.getByTestId("secret-value-input"), { target: { value: "s3cret-value" } });
    fireEvent.click(screen.getByTestId("secret-value-set"));

    await waitFor(() => expect(screen.getByTestId("secret-value-line").textContent).toContain("stored encrypted"));
    expect(api.setSecretValue).toHaveBeenCalledWith("gh_token", "s3cret-value");
    expect((screen.getByTestId("secret-value-input") as HTMLInputElement).value).toBe("");
  });

  it("names the missing master key", async () => {
    api.fetchSecretValues.mockResolvedValue({ ...view, masterKeyPresent: false, masterKeyProblem: "No master key is configured: set AGENTSMITH_SECRET_MASTER_KEY" });
    render(<SecretValueField name="gh_token" />);

    await waitFor(() => expect(screen.getByTestId("secret-value-no-key").textContent).toContain("AGENTSMITH_SECRET_MASTER_KEY"));
  });
});
