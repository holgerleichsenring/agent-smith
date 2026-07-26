import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { PrButton } from "../PrButton";

// p0372: THE pull-request button — draft vs open, clickability and styling are
// decided in this one component for every surface (list, outcome, full
// pipeline). A draft PR still has a valid URL, so it is ALWAYS a real anchor.

describe("PrButton", () => {
  it("PrButton_DraftPr_IsClickable", () => {
    render(<PrButton url="https://az/server/pr/7" isDraft testId="pr" />);
    const button = screen.getByTestId("pr");
    expect(button.tagName).toBe("A");
    expect(button).toHaveAttribute("href", "https://az/server/pr/7");
    expect(button).toHaveAttribute("target", "_blank");
    expect(button).not.toHaveAttribute("aria-disabled");
    expect(button).toHaveTextContent("Draft pull request");
    expect(button.className).toContain("draft");
  });

  it("PrButton_OpenPr_LabelsWithoutDraft_AndPrefixesRepo", () => {
    render(<PrButton url="https://az/web/pr/2" repo="web" testId="pr" />);
    const button = screen.getByTestId("pr");
    expect(button).toHaveTextContent("web:");
    expect(button).toHaveTextContent("Pull request");
    expect(button).not.toHaveTextContent("Draft");
    expect(button.className).not.toContain("draft");
  });
});
