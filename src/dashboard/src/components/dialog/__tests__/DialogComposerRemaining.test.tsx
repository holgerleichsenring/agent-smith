import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogComposer } from "../DialogComposer";

// 2026-10-08-e8b9h: a pick bigger than what the conversation has left under its byte cap is
// refused before a byte is sent, naming both sizes — the image, the single file and the card.
function sized(name: string, bytes: number, path?: string): File {
  const file = new File([new Uint8Array(bytes)], name, { type: "image/png" });
  if (path) Object.defineProperty(file, "webkitRelativePath", { value: path });
  return file;
}

describe("DialogComposer, the conversation's remaining bytes", () => {
  it("DialogComposer_PickOverRemaining_RefusedBeforeSending", async () => {
    const onAttach = vi.fn();
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={onAttach} onAttachSite={onAttachSite} left={1024} />);

    fireEvent.change(screen.getByTestId("dialog-composer-image"), { target: { files: [sized("shot.png", 4096)] } });
    expect(onAttach).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-composer-upload-note").textContent)
      .toBe("4 KB is more than the 1 KB this conversation has left. Remove an upload in the Uploads tab to make room.");

    fireEvent.change(screen.getByTestId("dialog-composer-files"), { target: { files: [sized("a.pdf", 2048)] } });
    expect(onAttachSite).not.toHaveBeenCalled();

    fireEvent.change(screen.getByTestId("dialog-composer-folder"), {
      target: { files: [sized("a.png", 900, "site/a.png"), sized("b.png", 900, "site/b.png")] },
    });
    expect((await screen.findByTestId("dialog-reference-selection-total")).textContent)
      .toContain("where the conversation has 1 KB left");
    expect(screen.getByTestId("dialog-reference-selection-send")).toBeDisabled();
  });

  it("DialogComposer_NoConversationYet_NothingPreChecked", () => {
    const onAttach = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={onAttach} onAttachSite={vi.fn()} left={null} />);

    fireEvent.change(screen.getByTestId("dialog-composer-image"), { target: { files: [sized("shot.png", 4096)] } });

    expect(onAttach).toHaveBeenCalled();
  });
});
