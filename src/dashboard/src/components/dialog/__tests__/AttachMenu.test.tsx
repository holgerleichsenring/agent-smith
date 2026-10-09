import { describe, it, expect, vi } from "vitest";
import { readFileSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { fireEvent, render, screen } from "@testing-library/react";
import { DialogComposer } from "../DialogComposer";

// 2026-10-09-86e1: "das menu ist verdeckt" — the card that holds the composer no longer clips it,
// the menu opens downward when there is no room above, and it is walked by keyboard.
const here = dirname(fileURLToPath(import.meta.url));

describe("The attach menu", () => {
  it("AttachMenu_EmptyConversation_AllEntriesVisible", () => {
    const css = readFileSync(join(here, "../../../styles/mock-parity.css"), "utf8");
    const surface = readFileSync(join(here, "../SpecDialogSurface.tsx"), "utf8");

    expect(css).toMatch(/\.mock-dialog \.ecard\.unclipped \{ overflow: visible; \}/);
    expect(surface).toContain('<section className="ecard inert unclipped min-w-0">');
    expect(css).toMatch(/\.mock-dialog \.d-menu\.down \{ top: calc\(100% \+ 6px\); bottom: auto; \}/);
  });

  it("AttachMenu_NoRoomAbove_OpensDownward", () => {
    const rect = vi.spyOn(HTMLElement.prototype, "getBoundingClientRect")
      .mockReturnValue({ top: -40, bottom: 80, left: 0, right: 0, width: 0, height: 120, x: 0, y: -40, toJSON: () => ({}) });
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} />);

    fireEvent.click(screen.getByTestId("dialog-composer-attach"));

    expect(screen.getByTestId("dialog-composer-attach-menu")).toHaveAttribute("data-direction", "down");
    rect.mockRestore();
  });

  it("AttachMenu_Keyboard_ArrowsWalkAndEscapeReturnsFocus", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} />);
    const plus = screen.getByTestId("dialog-composer-attach");

    fireEvent.click(plus);
    expect(screen.getByTestId("dialog-composer-attach-image")).toHaveFocus();
    fireEvent.keyDown(screen.getByTestId("dialog-composer-attach-menu"), { key: "ArrowDown" });
    expect(screen.getByTestId("dialog-composer-attach-files")).toHaveFocus();
    fireEvent.keyDown(screen.getByTestId("dialog-composer-attach-menu"), { key: "End" });
    expect(screen.getByTestId("dialog-composer-attach-folder")).toHaveFocus();
    fireEvent.keyDown(screen.getByTestId("dialog-composer-attach-menu"), { key: "ArrowDown" });
    expect(screen.getByTestId("dialog-composer-attach-image")).toHaveFocus();
    fireEvent.keyDown(document, { key: "Escape" });

    expect(screen.queryByTestId("dialog-composer-attach-menu")).not.toBeInTheDocument();
    expect(plus).toHaveFocus();
  });
});
