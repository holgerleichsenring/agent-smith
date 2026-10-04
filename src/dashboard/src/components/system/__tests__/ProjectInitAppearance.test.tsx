import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen } from "@testing-library/react";
import { ProjectInitAction } from "../ProjectInitAction";

// p0497: the card's own vocabulary. These assertions can show that the toggle draws
// from the studio's accent token instead of the operating system's, and that the pair
// is one group rather than something interleaved with the metadata. They cannot show
// that it LOOKS right — the operator's eye is the acceptance test, and the phase says so.

vi.mock("@/lib/projectInitApi", () => ({
  startProjectInit: vi.fn(async () => ({ runId: "2026-08-21T00-00-00-aaaa" })),
  // 2026-10-02-5f89d: no live init — the button renders as it always did.
  fetchProjectInit: vi.fn(async () => null),
}));

const box = () => screen.getByTestId("project-init-auto-accept-box-sample");
const input = () => screen.getByTestId("project-init-auto-accept-sample") as HTMLInputElement;
const refreshBox = () => screen.getByTestId("project-init-refresh-principles-box-sample");
const refreshInput = () =>
  screen.getByTestId("project-init-refresh-principles-sample") as HTMLInputElement;

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

  // 2026-10-04-2bf2: the refresh chip is the same component, so it wears the same tokens.
  it("RefreshPrinciplesChip_DefaultsOff_AndWearsTheAccentOnlyWhenTicked", () => {
    render(<ProjectInitAction project="sample" />);

    expect(refreshInput().checked).toBe(false);
    expect(refreshBox().getAttribute("style")).not.toContain("var(--accent)");

    fireEvent.click(refreshInput());

    expect(refreshInput().checked).toBe(true);
    expect(refreshBox().getAttribute("style")).toContain("var(--accent)");
  });

  it("InitOptions_BothChips_FormOneLabelledGroupInsideTheAction", () => {
    render(<ProjectInitAction project="sample" />);

    const options = screen.getByRole("group", { name: "Initialization options" });
    expect(options).toContainElement(input());
    expect(options).toContainElement(refreshInput());
    expect(screen.getByTestId("project-init-group-sample")).toContainElement(options);
    expect(refreshInput().closest("label")?.getAttribute("title")).toMatch(
      /core, language delta and framework overlays.*Project Specifics section is kept/,
    );
  });

  it("InitOptions_TickingOne_NeverDisablesTheOther", () => {
    render(<ProjectInitAction project="sample" />);

    fireEvent.click(refreshInput());
    expect(input().disabled).toBe(false);
    expect(input().checked).toBe(true);
    fireEvent.click(input());
    expect(refreshInput().disabled).toBe(false);
    expect(refreshInput().checked).toBe(true);
  });
});
