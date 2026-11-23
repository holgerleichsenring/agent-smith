import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";

// 2026-11-23-78d7: a new secret takes its value in the create drawer. Create with a value is one secret run (which
// adds the catalog secret), Create without one posts the catalog secret, and the field is cleared once sent.

const secretValues = vi.hoisted(() => ({
  fetchSecretValues: vi.fn(),
  setSecretValue: vi.fn(),
  deleteSecretValue: vi.fn(),
  rewrapSecretValues: vi.fn(),
}));
vi.mock("@/lib/secretValuesApi", () => secretValues);

const secretsCrud = vi.hoisted(() => ({
  list: vi.fn(),
  create: vi.fn(),
  update: vi.fn(),
  remove: vi.fn(),
}));
vi.mock("@/lib/configApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/configApi")>()),
  secretsApi: secretsCrud,
}));

import { EntityDrawer } from "../EntityDrawer";
import type { ConfigCatalog } from "../useConfigCatalog";

const CATALOG = {
  agents: [], trackers: [], connections: [], repos: [], projects: [], "mcp-servers": [], "design-sources": [], secrets: [],
} as unknown as ConfigCatalog;

const VIEW = { masterKeyPresent: true, masterKeyProblem: null, masterKeySource: null, secrets: [], orphans: [] };

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function openNew(onSaved = vi.fn()) {
  render(
    <EntityDrawer kind="secrets" initial={{ id: "" }} isNew catalog={CATALOG} capabilities={null}
      onClose={() => {}} onSaved={onSaved} />,
  );
  fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "DEPLOY_TOKEN" } });
  return onSaved;
}

describe("new secret drawer", () => {
  it("shows a write-only value input", async () => {
    secretValues.fetchSecretValues.mockResolvedValue(VIEW);
    openNew();
    const input = screen.getByTestId("secret-new-value-input") as HTMLInputElement;
    expect(input.type).toBe("password");
    await waitFor(() => expect(input.disabled).toBe(false));
    expect(screen.queryByTestId("secret-value-field")).toBeNull();
  });

  it("sends the value through the secret run on Create and clears the field", async () => {
    secretValues.fetchSecretValues.mockResolvedValue(VIEW);
    secretValues.setSecretValue.mockResolvedValue({ line: "stored" });
    const onSaved = openNew();

    fireEvent.change(screen.getByTestId("secret-new-value-input"), { target: { value: "s3cret-value" } });
    fireEvent.click(screen.getByTestId("config-drawer-save"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(secretValues.setSecretValue).toHaveBeenCalledWith("DEPLOY_TOKEN", "s3cret-value");
    expect(secretsCrud.create).not.toHaveBeenCalled();
    expect((screen.getByTestId("secret-new-value-input") as HTMLInputElement).value).toBe("");
  });

  it("keeps the drawer open with the server's line when the value is refused", async () => {
    secretValues.fetchSecretValues.mockResolvedValue(VIEW);
    secretValues.setSecretValue.mockRejectedValue(new Error("A value for 'DEPLOY_TOKEN' must be at least 6 characters"));
    const onSaved = openNew();

    fireEvent.change(screen.getByTestId("secret-new-value-input"), { target: { value: "abc" } });
    fireEvent.click(screen.getByTestId("config-drawer-save"));

    await waitFor(() => expect(screen.getByTestId("config-drawer-error").textContent).toContain("at least 6 characters"));
    expect(onSaved).not.toHaveBeenCalled();
    expect((screen.getByTestId("secret-new-value-input") as HTMLInputElement).value).toBe("");
  });

  it("posts the catalog secret alone when no value is given", async () => {
    secretValues.fetchSecretValues.mockResolvedValue(VIEW);
    secretsCrud.create.mockResolvedValue({ id: "DEPLOY_TOKEN" });
    const onSaved = openNew();

    fireEvent.click(screen.getByTestId("config-drawer-save"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(secretsCrud.create).toHaveBeenCalledWith({ id: "DEPLOY_TOKEN" });
    expect(secretValues.setSecretValue).not.toHaveBeenCalled();
  });

  it("disables the value and says why when no master key is configured", async () => {
    secretValues.fetchSecretValues.mockResolvedValue({ ...VIEW, masterKeyPresent: false });
    openNew();

    await waitFor(() => expect(screen.getByTestId("secret-new-value-help").textContent).toContain("No master key"));
    expect((screen.getByTestId("secret-new-value-input") as HTMLInputElement).disabled).toBe(true);
  });
});
