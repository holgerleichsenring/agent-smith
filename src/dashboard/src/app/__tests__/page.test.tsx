import { render, screen } from "@testing-library/react";
import JobsPage from "../page";

describe("JobsPage (root /)", () => {
  it("renders the agent-smith heading", () => {
    render(<JobsPage />);
    expect(screen.getByRole("heading", { name: /agent-smith/i })).toBeInTheDocument();
  });

  it("renders the p0169e connecting shim", () => {
    render(<JobsPage />);
    expect(screen.getByText(/connecting…/)).toBeInTheDocument();
  });
});
