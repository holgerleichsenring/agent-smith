import { useRef } from "react";
import { render, screen, fireEvent, cleanup, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, afterEach } from "vitest";

import { ConfirmDialog, useConfirmDialog } from "../ConfirmDialog";

// 2026-09-20-4b0ab: the confirmation the page draws, pinned where the platform gives it
// nothing. jsdom 25 constructs an HTMLDialogElement and implements NEITHER showModal nor
// close, fires no cancel event and runs no close watcher — so every behaviour asserted here is
// the component's own, and the one thing the environment cannot show (that the element is
// opened MODALLY) is proved by standing a showModal in for the missing one that throws exactly
// where the platform's throws.

afterEach(() => {
  cleanup();
  const proto = window.HTMLDialogElement.prototype as unknown as Record<string, unknown>;
  delete proto.showModal;
  delete proto.close;
});

/** A showModal that behaves as the platform's does in the one way that matters: it THROWS on
 *  an element that already carries the open attribute. A component that rendered the element
 *  open and then called showModal would take that throw here, as it would in a browser. */
function standInForShowModal(): { calls: number; openWhenCalled: boolean[] } {
  const seen = { calls: 0, openWhenCalled: [] as boolean[] };
  const proto = window.HTMLDialogElement.prototype as unknown as Record<string, unknown>;
  proto.showModal = function showModal(this: HTMLDialogElement) {
    seen.openWhenCalled.push(this.hasAttribute("open"));
    if (this.hasAttribute("open")) throw new Error("InvalidStateError: the dialog is already open");
    seen.calls += 1;
    this.setAttribute("open", "");
  };
  proto.close = function close(this: HTMLDialogElement) {
    this.removeAttribute("open");
  };
  return seen;
}

const WARNING = "Delete “a widget that reads the ledger”?\n\n"
  + "This cannot be undone: the conversation and every answer you gave in it are deleted.\n\n"
  + "Whatever it filed stays in the tracker.";

describe("The confirmation the page draws", () => {
  it("ConfirmDialog_WhenNotAsking_RendersNothing", () => {
    render(<ConfirmDialog ask={null} onResolve={vi.fn()} />);

    expect(screen.queryByTestId("confirm-dialog")).toBeNull();
    expect(document.querySelector("dialog")).toBeNull();
  });

  // The order is the whole decision: showModal throws on an element that already carries open,
  // so the element is rendered WITHOUT it and opened from the effect. Reverse that and the
  // stand-in above throws, exactly as the platform would.
  it("ConfirmDialog_WhenAsking_OpensModallyRatherThanByTheOpenAttribute", () => {
    const seen = standInForShowModal();

    render(<ConfirmDialog ask={WARNING} onResolve={vi.fn()} />);

    expect(seen.calls).toBe(1);
    // It was called on an element that was NOT yet open — which is the only way it can be
    // called at all, and is what the fallback ordering would have got wrong.
    expect(seen.openWhenCalled).toEqual([false]);
    expect(screen.getByTestId("confirm-dialog")).toHaveAttribute("open");

    // And where there is no showModal at all, the attribute is the fallback, so the bare
    // environment still renders a dialog that is on the page rather than a hidden one.
    cleanup();
    const proto = window.HTMLDialogElement.prototype as unknown as Record<string, unknown>;
    delete proto.showModal;
    render(<ConfirmDialog ask={WARNING} onResolve={vi.fn()} />);
    expect(seen.calls).toBe(1);
    expect(screen.getByTestId("confirm-dialog")).toHaveAttribute("open");
  });

  it("ConfirmDialog_Cancel_ReportsNoConfirmation", () => {
    const resolved = vi.fn();
    render(<ConfirmDialog ask={WARNING} cancelLabel="Keep it" onResolve={resolved} />);

    // The warning is READABLE on the page — every paragraph of it, which is the point of
    // taking it off the browser's own prompt.
    expect(screen.getByTestId("confirm-dialog")).toHaveTextContent(
      "Delete “a widget that reads the ledger”?");
    expect(screen.getByTestId("confirm-dialog")).toHaveTextContent("This cannot be undone");
    expect(screen.getByTestId("confirm-dialog")).toHaveTextContent(
      "Whatever it filed stays in the tracker.");

    fireEvent.click(screen.getByText("Keep it"));

    expect(resolved).toHaveBeenCalledTimes(1);
    expect(resolved).toHaveBeenCalledWith(false);
  });

  it("ConfirmDialog_Confirm_ReportsOne", () => {
    const resolved = vi.fn();
    render(<ConfirmDialog ask={WARNING} confirmLabel="Delete" onResolve={resolved} />);

    fireEvent.click(screen.getByText("Delete"));

    expect(resolved).toHaveBeenCalledTimes(1);
    expect(resolved).toHaveBeenCalledWith(true);
  });

  // No close watcher here and no cancel event, so this can only pass against a key handler the
  // component owns — which is also what a browser needs, because the close watcher would
  // otherwise close the element while the page still believed it was asking.
  it("ConfirmDialog_Escape_CancelsLikeTheCancelButton", () => {
    const resolved = vi.fn();
    render(<ConfirmDialog ask={WARNING} onResolve={resolved} />);

    fireEvent.keyDown(screen.getByTestId("confirm-dialog"), { key: "Escape" });

    expect(resolved).toHaveBeenCalledTimes(1);
    expect(resolved).toHaveBeenCalledWith(false);

    // Another key is not a dismissal.
    fireEvent.keyDown(screen.getByTestId("confirm-dialog"), { key: "Enter" });
    expect(resolved).toHaveBeenCalledTimes(1);

    // And in a browser, where a close watcher DOES exist, the same dismissal arrives as a
    // cancel event instead. It is answered the same way and the native close is refused, so
    // the element cannot go away while this page still believes it is asking.
    cleanup();
    const second = vi.fn();
    render(<ConfirmDialog ask={WARNING} onResolve={second} />);
    const cancelled = new Event("cancel", { bubbles: false, cancelable: true });
    screen.getByTestId("confirm-dialog").dispatchEvent(cancelled);
    expect(second).toHaveBeenCalledWith(false);
    expect(cancelled.defaultPrevented).toBe(true);
  });

  // The PROMISE-shaped ask, which is what the two surfaces still on window.confirm would need:
  // both decide synchronously inside an async file handler, and a callback-shaped confirmation
  // would make each of them hold the pending file in state.
  it("ConfirmDialog_ThePromiseShapedAsk_SettlesWithTheAnswerGiven", async () => {
    const settled = vi.fn();
    render(<Host onSettled={settled} />);

    fireEvent.click(screen.getByTestId("host-ask"));
    fireEvent.click(await screen.findByTestId("confirm-dialog-confirm"));
    await waitFor(() => expect(settled).toHaveBeenCalledWith([true]));
    expect(screen.queryByTestId("confirm-dialog")).toBeNull();

    fireEvent.click(screen.getByTestId("host-ask"));
    fireEvent.click(await screen.findByTestId("confirm-dialog-cancel"));
    await waitFor(() => expect(settled).toHaveBeenCalledWith([true, false]));
  });

  // An ask that arrives while one is still open replaces it, and the one it replaced is
  // answered no rather than left on a promise nobody will ever settle.
  it("ConfirmDialog_AnAskThatSupersedesAnOpenOne_SettlesTheOlderOneNo", async () => {
    const settled = vi.fn();
    render(<Host onSettled={settled} />);

    fireEvent.click(screen.getByTestId("host-ask"));
    await screen.findByTestId("confirm-dialog");
    expect(settled).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId("host-ask"));

    await waitFor(() => expect(settled).toHaveBeenCalledWith([false]));
    // The second ask is still standing, waiting for its own answer.
    expect(screen.getByTestId("confirm-dialog")).toBeInTheDocument();
  });

  // A native dialog does not close on a backdrop click: the click targets the dialog element
  // itself, so dismissal is a handler comparing the target — and a click on the content inside
  // it must not dismiss, which is the half that makes the comparison worth writing.
  it("ConfirmDialog_ABackdropClick_Cancels", () => {
    const resolved = vi.fn();
    render(<ConfirmDialog ask={WARNING} onResolve={resolved} />);

    fireEvent.click(screen.getByText("Delete “a widget that reads the ledger”?"));
    expect(resolved).not.toHaveBeenCalled();

    fireEvent.click(screen.getByTestId("confirm-dialog"));

    expect(resolved).toHaveBeenCalledTimes(1);
    expect(resolved).toHaveBeenCalledWith(false);
  });
});

/** A caller of the promise-shaped ask: it asks, awaits, and records what it was told. */
function Host({ onSettled }: { onSettled: (answers: boolean[]) => void }) {
  const confirmation = useConfirmDialog();
  const answers = useRef<boolean[]>([]);
  return (
    <>
      <button
        type="button"
        data-testid="host-ask"
        onClick={() => {
          void confirmation.ask(WARNING).then((given) => {
            answers.current = [...answers.current, given];
            onSettled(answers.current);
          });
        }}
      >
        ask
      </button>
      <ConfirmDialog {...confirmation.dialog} />
    </>
  );
}
