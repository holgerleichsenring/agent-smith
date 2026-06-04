import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { SectionLabel } from "../SectionLabel";

describe("SectionLabel", () => {
  it("SectionLabel_RendersEyebrowUppercaseToken", () => {
    render(<SectionLabel testId="label">System</SectionLabel>);
    const label = screen.getByTestId("label");
    expect(label.className).toContain("eyebrow-uppercase");
    expect(label).toHaveTextContent("System");
  });
});
