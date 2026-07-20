import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { ChangesView } from "../ChangesView";
import type { ConfigChange } from "@/lib/configApi";

// entities.ts (imported transitively) needs the CRUD client exports to exist,
// so the mock provides inert clients plus the changes endpoints under test.
const fetchChanges = vi.fn();
const revertChange = vi.fn();

vi.mock("@/lib/configApi", () => {
  const inert = { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn() };
  return {
    agentsApi: inert,
    trackersApi: inert,
    connectionsApi: inert,
    reposApi: inert,
    projectsApi: inert,
    mcpServersApi: inert,
    secretsApi: inert,
    fetchChanges: (...a: unknown[]) => fetchChanges(...a),
    revertChange: (...a: unknown[]) => revertChange(...a),
  };
});

const CHANGES: ConfigChange[] = [
  {
    id: "chg-1",
    actor: "holger",
    timestampUtc: "2026-07-16T10:00:00Z",
    entityKind: "projects",
    entityId: "checkout",
    action: "update",
    fields: [{ field: "agent", before: "gpt4", after: "gpt5" }],
    reverted: false,
  },
  {
    id: "chg-2",
    actor: "system",
    timestampUtc: "2026-07-16T09:00:00Z",
    entityKind: "agents",
    entityId: "gpt5",
    action: "create",
    fields: [{ field: "provider", before: null, after: "openai" }],
    reverted: true,
  },
  {
    id: "chg-3",
    actor: "holger",
    timestampUtc: "2026-07-16T11:00:00Z",
    entityKind: "settings",
    entityId: "orchestrator",
    action: "update",
    fields: [{ field: "maxRunWallTimeSeconds", before: "1800", after: "5400" }],
    reverted: false,
  },
];

beforeEach(() => {
  fetchChanges.mockReset();
  revertChange.mockReset();
});

describe("ChangesView", () => {
  it("ChangesView_AttributedRows_RenderWhoWhenAndDiff", async () => {
    fetchChanges.mockResolvedValue(CHANGES);
    render(<ChangesView />);
    await screen.findByTestId("config-change-chg-1");

    expect(screen.getByTestId("config-change-who-chg-1")).toHaveTextContent("holger");
    expect(screen.getByTestId("config-change-action-chg-1")).toHaveTextContent("update");
    // The diff shows before → after.
    const diff = screen.getByTestId("config-change-diff-chg-1-agent");
    expect(diff).toHaveTextContent("gpt4");
    expect(diff).toHaveTextContent("gpt5");
  });

  it("ChangesView_RevertRow_PostsRevertAndReloads", async () => {
    fetchChanges.mockResolvedValue(CHANGES);
    revertChange.mockResolvedValue(undefined);
    render(<ChangesView />);
    await screen.findByTestId("config-change-chg-1");

    fireEvent.click(screen.getByTestId("config-change-revert-chg-1"));

    await waitFor(() => expect(revertChange).toHaveBeenCalledWith("chg-1"));
    // Feed reloads after a revert (initial load + reload).
    expect(fetchChanges).toHaveBeenCalledTimes(2);
  });

  it("ChangesView_SettingsRow_RendersSettingsKindKeyedByTypeAndReverts", async () => {
    fetchChanges.mockResolvedValue(CHANGES);
    revertChange.mockResolvedValue(undefined);
    render(<ChangesView />);
    const row = await screen.findByTestId("config-change-chg-3");

    // The settings singleton renders as "Settings / <type>" with its field diff.
    expect(row).toHaveTextContent("Settings / orchestrator");
    const diff = screen.getByTestId("config-change-diff-chg-3-maxRunWallTimeSeconds");
    expect(diff).toHaveTextContent("1800");
    expect(diff).toHaveTextContent("5400");

    fireEvent.click(screen.getByTestId("config-change-revert-chg-3"));
    await waitFor(() => expect(revertChange).toHaveBeenCalledWith("chg-3"));
  });

  it("ChangesView_AlreadyReverted_ShowsNoRevertButton", async () => {
    fetchChanges.mockResolvedValue(CHANGES);
    render(<ChangesView />);
    await screen.findByTestId("config-change-chg-2");
    expect(screen.queryByTestId("config-change-revert-chg-2")).toBeNull();
    expect(screen.getByTestId("config-change-reverted-chg-2")).toBeInTheDocument();
  });
});
