import { render, screen } from "@testing-library/react";
import { vi } from "vitest";
import JobsPage from "../page";

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    // 1 = HubConnectionState.Connected per @microsoft/signalr enum
    connectionState: 1,
    overview: { active: [], recent: [], systemActivity: null },
  }),
}));

describe("JobsPage (root /)", () => {
  it("renders the Runs heading", () => {
    render(<JobsPage />);
    expect(screen.getByRole("heading", { name: /runs/i })).toBeInTheDocument();
  });

  it("mounts the runs list (empty state when no runs)", () => {
    render(<JobsPage />);
    expect(screen.getByTestId("runs-empty")).toBeInTheDocument();
  });
});
