import { render, screen } from "@testing-library/react";
import LandingPage from "../page";

describe("LandingPage", () => {
  it("renders the agent-smith brand heading", () => {
    render(<LandingPage />);
    expect(screen.getByRole("heading", { name: /agent-smith/i })).toBeInTheDocument();
  });

  it("notes that the Job-Viewer ships in p0169a", () => {
    render(<LandingPage />);
    expect(screen.getByText(/p0169a/i)).toBeInTheDocument();
  });
});
