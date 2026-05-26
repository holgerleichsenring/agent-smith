import { render, screen } from "@testing-library/react";
import { vi } from "vitest";
import JobsPage from "../page";

vi.mock("@/lib/api", () => ({
  listJobs: vi.fn().mockResolvedValue({ jobs: [], total: 0, page: 1, pageSize: 50 }),
}));

describe("JobsPage (root /)", () => {
  it("renders the agent-smith heading", async () => {
    const ui = await JobsPage();
    render(ui);
    expect(screen.getByRole("heading", { name: /agent-smith/i })).toBeInTheDocument();
  });

  it("renders empty-state when there are no jobs", async () => {
    const ui = await JobsPage();
    render(ui);
    expect(screen.getByTestId("empty-state")).toBeInTheDocument();
  });
});
