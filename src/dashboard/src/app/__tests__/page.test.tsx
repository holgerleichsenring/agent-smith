import { render, screen } from "@testing-library/react";
import { vi } from "vitest";
import JobsPage from "../page";

vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    client: {},
    // 1 = HubConnectionState.Connected per @microsoft/signalr enum
    connectionState: 1,
    overview: { active: [], recent: [] },
  }),
}));

describe("JobsPage (root /)", () => {
  it("renders the agent-smith heading", () => {
    render(<JobsPage />);
    expect(screen.getByRole("heading", { name: /agent-smith/i })).toBeInTheDocument();
  });

  it("mounts the overview grid (empty state when no runs)", () => {
    render(<JobsPage />);
    expect(screen.getByTestId("overview-empty")).toBeInTheDocument();
  });
});
