import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { TrailExpiredEmptyState } from "../TrailExpiredEmptyState";

describe("TrailExpiredEmptyState", () => {
  it("renders the expired heading", () => {
    render(<TrailExpiredEmptyState prUrl={null} />);
    expect(screen.getByTestId("trail-expired-empty")).toBeInTheDocument();
    expect(screen.getByText(/trail expired/i)).toBeInTheDocument();
  });

  it("with prUrl shows view-in-PR link", () => {
    render(<TrailExpiredEmptyState prUrl="https://example.com/pr/42" />);
    const link = screen.getByTestId("trail-expired-pr-link");
    expect(link).toHaveAttribute("href", "https://example.com/pr/42");
    expect(link).toHaveAttribute("target", "_blank");
  });

  it("without prUrl shows generic expired message, no link", () => {
    render(<TrailExpiredEmptyState prUrl={null} />);
    expect(screen.queryByTestId("trail-expired-pr-link")).not.toBeInTheDocument();
    expect(screen.getByText(/result.md/i)).toBeInTheDocument();
  });
});
