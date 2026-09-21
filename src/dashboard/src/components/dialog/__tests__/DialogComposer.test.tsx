import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogComposer } from "../DialogComposer";

// 2026-09-20-4b0ac: the composer is rendered on its own here. Every behaviour it has was
// tested through the Work it out surface until now, whose suite mounts the whole page to reach
// it; the arrangement of its two controls, and the menu the plus opens, are the composer's own
// and are read more easily where nothing else is on the screen. The names begin with the
// component so a reader can tell which suite a failure came from.

function renderComposer(over: Partial<Parameters<typeof DialogComposer>[0]> = {}) {
  const onSend = vi.fn();
  const onAttach = vi.fn();
  render(<DialogComposer onSend={onSend} onAttach={onAttach} {...over} />);
  return { onSend, onAttach };
}

/** Every click the hidden file input receives. The picker cannot be opened in jsdom, and what
 *  matters is that the control the browser would open it for was clicked. */
function watchPicker(): () => number {
  const clicks = vi.fn();
  screen.getByTestId("dialog-composer-image").addEventListener("click", clicks);
  return () => clicks.mock.calls.length;
}

describe("DialogComposer", () => {
  it("DialogComposer_TheAttachControl_IsNamedAttachAndSitsBeforeTheTextarea", () => {
    renderComposer();

    const plus = screen.getByRole("button", { name: "Attach" });
    const text = screen.getByTestId("dialog-composer-text");
    expect(plus).toHaveAttribute("aria-haspopup", "menu");
    // The lower LEFT: the row is a flex row in document order, so what precedes the writing
    // area in the DOM is what sits to the left of it.
    expect(plus.compareDocumentPosition(text) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("DialogComposer_TheAttachControl_OpensAMenuRatherThanThePickerDirectly", () => {
    renderComposer();
    const picked = watchPicker();

    expect(screen.queryByRole("menu")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Attach" }));

    expect(screen.getByRole("menu")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Attach" })).toHaveAttribute("aria-expanded", "true");
    // A plus that always opened the same file dialog would be an image button wearing a plus.
    expect(picked()).toBe(0);
  });

  it("DialogComposer_TheMenuEntry_PicksAnImage", () => {
    renderComposer();
    const picked = watchPicker();

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Image" }));

    expect(picked()).toBe(1);
    // and the menu is gone: choosing is a way out of it as much as Escape is.
    expect(screen.queryByRole("menu")).toBeNull();
    // The accepted kinds are the picker's, unchanged by the control that opens it.
    expect(screen.getByTestId("dialog-composer-image"))
      .toHaveAttribute("accept", "image/png,image/jpeg,image/gif,image/webp");
  });

  it("DialogComposer_TheMenu_ClosesOnEscape", () => {
    renderComposer();

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));
    expect(screen.getByRole("menu")).toBeInTheDocument();

    fireEvent.keyDown(document, { key: "Escape" });

    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.getByRole("button", { name: "Attach" })).toHaveAttribute("aria-expanded", "false");
  });

  it("DialogComposer_TheMenu_ClosesOnAClickAway", () => {
    renderComposer();

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));
    // Inside the control is not away: the press that opened it must not close it again.
    fireEvent.mouseDown(screen.getByRole("menuitem", { name: "Image" }));
    expect(screen.getByRole("menu")).toBeInTheDocument();

    fireEvent.mouseDown(document.body);

    expect(screen.queryByRole("menu")).toBeNull();
  });

  it("DialogComposer_TheSendControl_IsNamedSend", () => {
    renderComposer();

    const send = screen.getByRole("button", { name: "Send" });
    const text = screen.getByTestId("dialog-composer-text");
    // The lower right: after the writing area, as the plus is before it.
    expect(text.compareDocumentPosition(send) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // The arrow says nothing to a screen reader; the button's name is what does.
    expect(send.querySelector("svg")).toHaveAttribute("aria-hidden", "true");
  });

  it("DialogComposer_Sending_StillClearsTheFieldAndReportsTheText", () => {
    const { onSend } = renderComposer();
    const text = screen.getByTestId("dialog-composer-text");

    fireEvent.change(text, { target: { value: "  a widget that reads the ledger  " } });
    fireEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(onSend).toHaveBeenCalledWith("a widget that reads the ledger");
    expect(text).toHaveValue("");
  });

  // Found while reviewing this phase's own diff: disabled is not only a state the composer is
  // rendered in, it is one it ARRIVES at — the session can close under it — and the hidden file
  // input carries no disabled of its own, so a menu left standing would still reach the picker.
  it("DialogComposer_WhenDisabledWhileTheMenuStandsOpen_TheMenuGoesWithIt", () => {
    const onAttach = vi.fn();
    const { rerender } = render(<DialogComposer onSend={vi.fn()} onAttach={onAttach} />);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));
    expect(screen.getByRole("menu")).toBeInTheDocument();

    rerender(<DialogComposer onSend={vi.fn()} onAttach={onAttach} disabled />);

    expect(screen.queryByRole("menu")).toBeNull();
    expect(screen.getByRole("button", { name: "Attach" })).toHaveAttribute("aria-expanded", "false");
  });

  it("DialogComposer_WhenDisabled_NeitherControlActs", () => {
    const { onSend } = renderComposer({ disabled: true, hint: "Pick a project first." });

    const plus = screen.getByRole("button", { name: "Attach" });
    const send = screen.getByRole("button", { name: "Send" });
    expect(plus).toBeDisabled();
    expect(send).toBeDisabled();

    fireEvent.click(plus);
    fireEvent.click(send);

    expect(screen.queryByRole("menu")).toBeNull();
    expect(onSend).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-composer-hint")).toHaveTextContent("Pick a project first.");
  });
});
