import { render, screen } from "@testing-library/react";
import { StatusBadge } from "../StatusBadge";

describe("StatusBadge", () => {
  it("renders green accent for status=done", () => {
    render(<StatusBadge status="done" />);
    const badge = screen.getByTestId("status-badge");
    expect(badge).toHaveTextContent("done");
    expect(badge.className).toMatch(/emerald/);
  });

  it("renders red accent for status=failed", () => {
    render(<StatusBadge status="failed" />);
    const badge = screen.getByTestId("status-badge");
    expect(badge.className).toMatch(/rose/);
  });

  it("falls back to neutral tone for unknown status", () => {
    render(<StatusBadge status="unknown" />);
    const badge = screen.getByTestId("status-badge");
    expect(badge.className).toMatch(/stone/);
  });
});
