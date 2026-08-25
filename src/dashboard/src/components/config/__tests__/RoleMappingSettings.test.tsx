import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { SettingsStudio } from "../SettingsStudio";
import { resetCapabilitiesCache } from "../useCapabilities";

// 2026-08-25-1806: the role mapping is edited like every other settings singleton, and a
// bundle is built by PICKING from the closed permission catalog the server serves. Typing
// a permission name is how an operator reaches the state the server reports as a finding
// and drops silently, so there is nowhere here to type one.

// Hoisted with the mock factory, because vi.mock is lifted above every const in the file.
const fixture = vi.hoisted(() => ({
  catalog: ["config.read", "config.write", "runs.read", "runs.control"],
  builtInRoles: ["admin", "operator", "reader"],
}));
const CATALOG = fixture.catalog;

const saveSetting = vi.fn((_key: string, value: unknown) => Promise.resolve(value));

vi.mock("@/lib/configApi", () => ({
  fetchSetting: vi.fn(() =>
    Promise.resolve({
      roleClaim: "roles",
      groupClaim: "groups",
      roles: { auditor: ["config.read"] },
      groupRoles: { "platform-admins": ["admin"] },
    }),
  ),
  saveSetting: (key: string, value: unknown) => saveSetting(key, value),
  fetchCapabilities: vi.fn().mockResolvedValue({
    trackerTypes: [],
    connectionTypes: [],
    agentProviders: [],
    resolutionStrategies: [],
    pipelines: [],
    roles: [],
    permissions: fixture.catalog,
    builtInRoles: fixture.builtInRoles,
  }),
}));

beforeEach(() => {
  vi.clearAllMocks();
  resetCapabilitiesCache();
});

describe("RoleMappingSettings", () => {
  it("Studio_ARoleBundle_OffersTheCatalogRatherThanFreeText", async () => {
    render(<SettingsStudio settingKey="role_mapping" />);
    const picks = await screen.findByTestId("setting-rolemapping-role-auditor-permissions");

    // Every catalogued permission is offered as a pick, and only what the catalog holds.
    for (const permission of CATALOG) {
      expect(
        screen.getByTestId(`setting-rolemapping-role-auditor-permissions-option-${permission}`),
      ).toBeInTheDocument();
    }
    expect(picks.querySelectorAll("button.pick")).toHaveLength(CATALOG.length);
    expect(picks.querySelectorAll("input")).toHaveLength(0);
  });

  it("Bundle_PickingAPermission_SavesTheEditedBundle", async () => {
    render(<SettingsStudio settingKey="role_mapping" />);
    await screen.findByTestId("setting-rolemapping-role-auditor-permissions");

    fireEvent.click(
      screen.getByTestId("setting-rolemapping-role-auditor-permissions-option-runs.read"),
    );
    fireEvent.click(screen.getByTestId("settings-save"));

    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith(
      "role_mapping",
      expect.objectContaining({ roles: { auditor: ["config.read", "runs.read"] } }),
    );
  });

  it("AddRole_ANameThatCollidesWithABuiltIn_IsRefusedBeforeItCanBeSaved", async () => {
    render(<SettingsStudio settingKey="role_mapping" />);
    await screen.findByTestId("setting-rolemapping-role-add-input");

    fireEvent.change(screen.getByTestId("setting-rolemapping-role-add-input"), {
      target: { value: "Admin" },
    });

    expect(screen.getByTestId("setting-rolemapping-role-add-collision")).toBeInTheDocument();
    expect(screen.getByTestId("setting-rolemapping-role-add-confirm")).toBeDisabled();
  });

  it("GroupMapping_GrantsRolesPickedFromTheKnownNames_NotTypedOnes", async () => {
    render(<SettingsStudio settingKey="role_mapping" />);
    const picks = await screen.findByTestId(
      "setting-rolemapping-group-platform-admins-roles",
    );

    // The built-in three plus the one custom role this mapping declares.
    expect(picks.querySelectorAll("button.pick")).toHaveLength(4);
    expect(
      screen.getByTestId("setting-rolemapping-group-platform-admins-roles-option-auditor"),
    ).toBeInTheDocument();
  });

  it("ClaimNames_AreEditableHere_NotOnlyInTheBootstrapFile", async () => {
    render(<SettingsStudio settingKey="role_mapping" />);
    const roleClaim = await screen.findByTestId("setting-rolemapping-roleclaim");
    expect(roleClaim).toHaveValue("roles");

    fireEvent.change(roleClaim, { target: { value: "app_roles" } });
    fireEvent.click(screen.getByTestId("settings-save"));

    await waitFor(() => expect(saveSetting).toHaveBeenCalledTimes(1));
    expect(saveSetting).toHaveBeenCalledWith(
      "role_mapping",
      expect.objectContaining({ roleClaim: "app_roles" }),
    );
  });
});
