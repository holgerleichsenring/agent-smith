import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ProjectInitAction } from "../ProjectInitAction";

// p0497: the card's own vocabulary. These assertions can show that the toggle draws
// from the studio's accent token instead of the operating system's, and that the pair
// is one group rather than something interleaved with the metadata. They cannot show
// that it LOOKS right — the operator's eye is the acceptance test, and the phase says so.

vi.mock("@/lib/projectInitApi", () => ({
  startProjectInit: vi.fn(async () => ({ runId: "2026-08-21T00-00-00-aaaa" })),
}));

const box = () => screen.getByTestId("project-init-auto-accept-box-sample");
const input = () => screen.getByTestId("project-init-auto-accept-sample") as HTMLInputElement;

describe("ProjectInitAction appearance", () => {
  it("AutoAcceptToggle_Checked_CarriesTheStudioAccentToken", () => {
    render(<ProjectInitAction project="sample" />);

    // Defaults to on (p0490), so the accent is what the tick box wears out of the box.
    expect(input().checked).toBe(true);
    expect(box().getAttribute("style")).toContain("var(--accent)");
  });

  it("AutoAcceptToggle_Unchecked_CarriesNoAccent", () => {
    render(<ProjectInitAction project="sample" />);

    fireEvent.click(input());

    expect(input().checked).toBe(false);
    expect(box().getAttribute("style")).not.toContain("var(--accent)");
  });

  it("AutoAcceptToggle_IsStillAnInput_AndStillToggles", () => {
    render(<ProjectInitAction project="sample" />);

    // The native input survives for accessibility and for every p0490 test that drives it.
    expect(input().tagName).toBe("INPUT");
    expect(input().type).toBe("checkbox");

    fireEvent.click(input());
    expect(input().checked).toBe(false);
    fireEvent.click(input());
    expect(input().checked).toBe(true);
  });

  it("InitAction_RendersAsOneActionGroup_SeparateFromTheTypeBadge", () => {
    render(<ProjectInitAction project="sample" />);

    const group = screen.getByTestId("project-init-group-sample");
    expect(group).toContainElement(screen.getByTestId("project-init-sample"));
    expect(group).toContainElement(input());
    // The separating rule is what makes the row read [actions] | [metadata].
    expect(group.getAttribute("style")).toContain("border-right");
  });

  it("InitAction_ExistingTestIds_AreUnchanged", () => {
    render(<ProjectInitAction project="sample" />);

    expect(screen.getByTestId("project-init-sample")).toBeInTheDocument();
    expect(screen.getByTestId("project-init-auto-accept-sample")).toBeInTheDocument();
  });
});
