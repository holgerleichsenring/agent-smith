import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ProjectMemorySection } from "../ProjectMemorySection";
import type { ProjectMemory } from "@/lib/projectMemoryApi";

// 2026-11-10-fcd7d: a project's memories in its panel — newest first as the server sends them, each with its
// standing, record and source, and a delete that takes the row away.

const api = vi.hoisted(() => ({
  fetchProjectMemories: vi.fn<(project: string, signal?: AbortSignal) => Promise<ProjectMemory[]>>(),
  deleteProjectMemory: vi.fn<(project: string, slug: string) => Promise<boolean>>(),
}));

vi.mock("@/lib/projectMemoryApi", () => api);

const memory = (slug: string, over: Partial<ProjectMemory> = {}): ProjectMemory => ({
  slug, type: "feedback", description: `${slug} description`, body: "b", standing: "unconfirmed",
  record: "no record yet", sourceKind: "conversation", sourceId: "conv-1", sourceRef: "4", author: "Holger",
  updatedAt: "2026-11-10T20:00:00Z", ...over,
});

describe("ProjectMemorySection", () => {
  beforeEach(() => {
    api.fetchProjectMemories.mockReset();
    api.deleteProjectMemory.mockReset();
  });

  it("ProjectMemorySection_ListsNewestFirstWithStandingAndProvenance", async () => {
    api.fetchProjectMemories.mockResolvedValue([
      memory("newer", { standing: "holding", record: "confirmed 2 · strained 0 · contradicted 0 · last 2026-11-09" }),
      memory("older", { sourceKind: "run", sourceId: "run-7", sourceRef: "2026-11-10-0001a", author: null }),
    ]);

    render(<ProjectMemorySection project="sample" />);

    const rows = await screen.findAllByTestId(/^project-memory-(newer|older)$/);
    expect(rows.map((r) => r.dataset.testid)).toEqual(["project-memory-newer", "project-memory-older"]);
    expect(rows[0].textContent).toContain("holding");
    expect(rows[0].textContent).toContain("confirmed 2");
    expect(screen.getByTestId("project-memory-source-newer").textContent).toBe("from conversation conv-1, turn 4, by Holger");
    expect(screen.getByTestId("project-memory-source-older").textContent).toBe("from run run-7, spec 2026-11-10-0001a");
    expect(api.fetchProjectMemories).toHaveBeenCalledWith("sample", expect.anything());
  });

  it("ProjectMemorySection_Body_IsFoldedUntilOpened", async () => {
    api.fetchProjectMemories.mockResolvedValue([memory("kept", { body: "Build the contracts first.\nThen the rest." })]);

    render(<ProjectMemorySection project="sample" />);

    const body = (await screen.findByTestId("project-memory-body-kept")) as HTMLDetailsElement;
    expect(body.open).toBe(false);
    expect(body.textContent).toContain("Build the contracts first.\nThen the rest.");
    fireEvent.click(screen.getByText("Full text"));
    expect(body.open).toBe(true);
  });

  it("ProjectMemorySection_Delete_RemovesTheRow", async () => {
    api.fetchProjectMemories.mockResolvedValue([memory("keep"), memory("drop")]);
    api.deleteProjectMemory.mockResolvedValue(true);

    render(<ProjectMemorySection project="sample" />);
    fireEvent.click(await screen.findByTestId("project-memory-delete-drop"));

    await waitFor(() => expect(screen.queryByTestId("project-memory-drop")).toBeNull());
    expect(screen.getByTestId("project-memory-keep")).toBeTruthy();
    expect(api.deleteProjectMemory).toHaveBeenCalledWith("sample", "drop");
  });

  it("ProjectMemorySection keeps the row when the delete is refused, and says nothing kept for an empty project", async () => {
    api.fetchProjectMemories.mockResolvedValueOnce([memory("keep")]);
    api.deleteProjectMemory.mockResolvedValue(false);

    const { unmount } = render(<ProjectMemorySection project="sample" />);
    fireEvent.click(await screen.findByTestId("project-memory-delete-keep"));
    await waitFor(() => expect(api.deleteProjectMemory).toHaveBeenCalled());
    expect(screen.getByTestId("project-memory-keep")).toBeTruthy();
    unmount();

    api.fetchProjectMemories.mockResolvedValueOnce([]);
    render(<ProjectMemorySection project="other" />);
    expect(await screen.findByTestId("project-memory-empty")).toBeTruthy();
  });
});
