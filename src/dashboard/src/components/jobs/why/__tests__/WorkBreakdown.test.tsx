import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { WorkBreakdown } from "../WorkBreakdown";

// p0341h: the panel answered "how much" and never "on what". These pin the two things a
// reader relies on: the heaviest work is on top, and a failure is visible without opening
// anything.
describe("WorkBreakdown", () => {
  const kinds = [
    { label: "dotnet build", count: 12, durationMs: 663_000, failed: 2 },
    { label: "grep", count: 187, durationMs: 130_000, failed: 0 },
  ];

  it("orders by time, so the row that cost the run its wall clock is first", () => {
    render(<WorkBreakdown title="Sandbox" subtitle="commands" kinds={kinds} testId="w" />);

    const rows = screen.getAllByTestId(/^work-row-/);
    expect(rows[0]).toHaveTextContent("dotnet build");
    expect(rows[0]).toHaveTextContent("12×");
    expect(rows[1]).toHaveTextContent("grep");
  });

  it("names the failures instead of leaving them to the exit codes below", () => {
    render(<WorkBreakdown title="Sandbox" subtitle="commands" kinds={kinds} testId="w" />);

    expect(screen.getByTestId("work-row-dotnet build")).toHaveTextContent("2 failed");
    expect(screen.getByTestId("work-row-grep")).not.toHaveTextContent("failed");
  });

  it("totals the section, so the two levels can be compared at a glance", () => {
    render(<WorkBreakdown title="Sandbox" subtitle="commands" kinds={kinds} testId="w" />);

    expect(screen.getByTestId("w")).toHaveTextContent("199 commands");
  });

  it("renders nothing when the run recorded no work of that kind", () => {
    const { container } = render(
      <WorkBreakdown title="Sandbox" subtitle="commands" kinds={[]} testId="w" />,
    );
    expect(container).toBeEmptyDOMElement();
  });
});
