import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ConfigStudio } from "../ConfigStudio";
import { ConfigCatalogProvider } from "../ConfigCatalogProvider";

// 2026-10-02-140d: the tracker and connection forms showed "Auth secret" twice — the
// descriptor's field as a free text box and a fixed secret picker bound to the same key.
// The descriptor now declares authSecret a SECRET, and the form renders that one field as
// a pick from the secrets catalog.

vi.mock("@/lib/configApi", () => {
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn().mockResolvedValue(rows[0] ?? { id: "x" }),
    update: vi.fn().mockResolvedValue(rows[0] ?? { id: "x" }),
    remove: vi.fn().mockResolvedValue(undefined),
  });
  return {
    agentsApi: client([]),
    trackersApi: client([]),
    connectionsApi: client([]),
    reposApi: client([]),
    projectsApi: client([]),
    mcpServersApi: client([]),
    designSourcesApi: client([]),
    secretsApi: client([{ id: "PAT" }, { id: "KEY" }]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    fetchInheritedSandbox: vi.fn().mockResolvedValue({ processWide: {}, projects: {} }),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
    validateProjectDraft: vi.fn().mockResolvedValue([]),
    validateTrackerDraft: vi.fn().mockResolvedValue([]),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [
        {
          type: "github",
          fields: [
            { key: "url", label: "Repository URL", required: true, kind: "text" },
            { key: "authSecret", label: "Auth secret", required: true, kind: "secret" },
          ],
        },
      ],
      connectionTypes: [
        {
          type: "gitlab",
          orgLabel: "group",
          fields: [
            { key: "organization", label: "Group", required: true, kind: "text" },
            { key: "authSecret", label: "Auth secret", required: true, kind: "secret" },
          ],
        },
      ],
      agentProviders: [],
      resolutionStrategies: [],
      permissions: [],
      builtInRoles: [],
      pipelines: [],
      roles: [],
    }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

beforeEach(() => vi.clearAllMocks());

async function openNew(section: "trackers" | "connections", type: string) {
  render(<ConfigCatalogProvider><ConfigStudio section={section} /></ConfigCatalogProvider>);
  fireEvent.click(await screen.findByTestId(`config-new-${section}`));
  const picker = screen.getByTestId("form-field-type");
  await waitFor(() => expect(picker.querySelector(`option[value="${type}"]`)).not.toBeNull());
  fireEvent.change(picker, { target: { value: type } });
  // The secrets catalog loads beside the capabilities; the pick fills once it has.
  await waitFor(() =>
    expect(screen.getByTestId("form-field-authSecret").querySelector('option[value="PAT"]')).not.toBeNull(),
  );
}

function expectOneSecretPick() {
  expect(screen.getAllByText(/auth secret/i)).toHaveLength(1);
  expect(screen.queryByTestId("form-ref-authSecret")).toBeNull();
  const secret = screen.getByTestId("form-field-authSecret");
  expect(secret.tagName).toBe("SELECT");
  expect(secret.querySelector('option[value="PAT"]')).not.toBeNull();
  expect(secret.querySelector('option[value="KEY"]')).not.toBeNull();
}

describe("Studio secret field (2026-10-02-140d)", () => {
  it("TrackerForm_AuthSecret_RendersOnceAsAPickFromTheSecretsCatalog", async () => {
    await openNew("trackers", "github");
    expectOneSecretPick();
  });

  it("ConnectionForm_AuthSecret_RendersOnceAsAPickFromTheSecretsCatalog", async () => {
    await openNew("connections", "gitlab");
    expectOneSecretPick();
  });

  it("ConnectionForm_RequiredSecret_IsMarkedAndBlocksSaveWhileEmpty", async () => {
    await openNew("connections", "gitlab");
    fireEvent.change(screen.getByTestId("form-field-id"), { target: { value: "gl" } });
    fireEvent.change(screen.getByTestId("form-field-organization"), { target: { value: "acme" } });

    const secret = screen.getByTestId("form-field-authSecret");
    expect(secret.closest(".field")?.querySelector(".req")).not.toBeNull();
    expect(screen.getByTestId("config-drawer-save")).toBeDisabled();

    fireEvent.change(secret, { target: { value: "PAT" } });
    expect(screen.getByTestId("config-drawer-save")).not.toBeDisabled();
  });
});
