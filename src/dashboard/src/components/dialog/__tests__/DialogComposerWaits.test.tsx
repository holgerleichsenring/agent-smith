import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createHash } from "node:crypto";
import { describe, expect, it, vi } from "vitest";
import { DialogComposer } from "../DialogComposer";
import type { SpecDialogHeldContent } from "@/types/spec-dialog";

// 2026-11-04-96a0b: a message sent ahead of its files starts a turn that never reads them, so Send
// waits while a pick is on screen or an upload is still going, says why, and keeps the text.
const sha = (text: string) => createHash("sha256").update(text).digest("hex");

function type(text: string) {
  fireEvent.change(screen.getByTestId("dialog-composer-text"), { target: { value: text } });
}

function pressEnter() {
  fireEvent.keyDown(screen.getByTestId("dialog-composer-text"), { key: "Enter" });
}

describe("DialogComposer, waiting for files", () => {
  it("DialogComposer: Send waits while the selection card is shown and keeps the text", async () => {
    const onSend = vi.fn();
    render(<DialogComposer onSend={onSend} onAttach={vi.fn()} onAttachSite={vi.fn()} />);
    fireEvent.change(screen.getByTestId("dialog-composer-files"), {
      target: { files: [new File(["a"], "a.html"), new File(["b"], "b.css")] },
    });
    await screen.findByTestId("dialog-reference-selection");

    type("what does this site do?");
    pressEnter();
    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    expect(onSend).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-composer-send")).toBeDisabled();
    expect(screen.getByTestId("dialog-composer-text")).toHaveValue("what does this site do?");
    expect(screen.getByTestId("dialog-composer-waiting")).toHaveTextContent("Send or cancel the selected files first.");
    fireEvent.click(screen.getByTestId("dialog-reference-selection-cancel"));
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    expect(onSend).toHaveBeenCalledWith("what does this site do?");
  });

  it("DialogComposer: Send waits while a held single file is shown, and while a single pick is still being checked", async () => {
    const onSend = vi.fn();
    let release: (held: SpecDialogHeldContent[]) => void = () => {};
    const loadHeld = () => new Promise<SpecDialogHeldContent[]>((resolve) => { release = resolve; });
    render(<DialogComposer onSend={onSend} onAttach={vi.fn()} onAttachSite={vi.fn()} loadHeld={loadHeld} />);
    type("and this brief?");

    fireEvent.change(screen.getByTestId("dialog-composer-files"), { target: { files: [new File(["# brief"], "copy.md")] } });

    expect(screen.getByTestId("dialog-composer-waiting")).toHaveTextContent("Waiting for the upload to finish.");
    pressEnter();
    expect(onSend).not.toHaveBeenCalled();
    await act(async () => release([{ sha256: sha("# brief"), setId: "a", name: "brief.md" }]));
    await screen.findByTestId("dialog-composer-held");
    expect(screen.getByTestId("dialog-composer-waiting")).toHaveTextContent("Send or cancel the selected files first.");
    expect(screen.getByTestId("dialog-composer-send")).toBeDisabled();
    expect(onSend).not.toHaveBeenCalled();
  });

  it("DialogComposer: Send waits while an upload is in flight and works once it settles", async () => {
    const onSend = vi.fn();
    let finish: () => void = () => {};
    const onAttachSite = vi.fn(() => new Promise<void>((resolve) => { finish = resolve; }));
    render(<DialogComposer onSend={onSend} onAttach={vi.fn()} onAttachSite={onAttachSite} />);
    fireEvent.change(screen.getByTestId("dialog-composer-files"), { target: { files: [new File(["z"], "site.zip")] } });
    await waitFor(() => expect(onAttachSite).toHaveBeenCalledTimes(1));
    type("what is in the zip?");

    fireEvent.click(screen.getByTestId("dialog-composer-send"));

    expect(onSend).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-composer-waiting")).toHaveTextContent("Waiting for the upload to finish.");
    await act(async () => finish());
    await waitFor(() => expect(screen.getByTestId("dialog-composer-send")).toBeEnabled());
    expect(screen.queryByTestId("dialog-composer-waiting")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("dialog-composer-send"));
    expect(onSend).toHaveBeenCalledWith("what is in the zip?");
  });
});
