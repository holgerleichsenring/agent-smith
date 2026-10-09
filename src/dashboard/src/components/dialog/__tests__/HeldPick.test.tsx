import { describe, it, expect, vi } from "vitest";
import { createHash } from "node:crypto";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DialogComposer } from "../DialogComposer";
import type { SpecDialogHeldContent } from "@/types/spec-dialog";

// 2026-10-09-86e1: "ich kann immer noch doppelte auswählen" — a picked file the conversation
// already holds, by content under any name, is shown as held before anything is sent.
const sha = (text: string) => createHash("sha256").update(text).digest("hex");
const held: SpecDialogHeldContent[] = [{ sha256: sha("# brief"), setId: "a", name: "brief.md" }];

function folderFile(text: string, path: string): File {
  const file = new File([text], path.split("/").at(-1)!);
  Object.defineProperty(file, "webkitRelativePath", { value: path });
  return file;
}

describe("Held files", () => {
  it("Composer_SingleHeldFile_NotSentAndSaysWhere", async () => {
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={onAttachSite} loadHeld={async () => held} />);

    fireEvent.change(screen.getByTestId("dialog-composer-files"), { target: { files: [new File(["# brief"], "copy.md")] } });

    expect((await screen.findByTestId("dialog-composer-held")).textContent)
      .toContain("copy.md is already in this conversation, in 'brief.md'. It was not sent.");
    expect(onAttachSite).not.toHaveBeenCalled();
    fireEvent.click(screen.getByTestId("dialog-composer-held-send"));
    expect(onAttachSite).toHaveBeenCalledTimes(1);
  });

  it("Composer_SingleNewFile_IsSentAtOnce", async () => {
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={onAttachSite} loadHeld={async () => held} />);

    fireEvent.change(screen.getByTestId("dialog-composer-files"), { target: { files: [new File(["new"], "new.md")] } });

    await waitFor(() => expect(onAttachSite).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId("dialog-composer-held")).not.toBeInTheDocument();
  });

  it("SelectionCard_HeldFile_UntickedAndMarked", async () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} loadHeld={async () => held} />);

    fireEvent.change(screen.getByTestId("dialog-composer-folder"), { target: { files: [
      folderFile("# brief", "v2/brief.md"), folderFile("<h1>", "v2/index.html"),
      folderFile("# brief", "v2/notes/again.md"), folderFile("new", "v2/notes/new.md"),
    ] } });

    const loose = await screen.findByTestId("dialog-reference-entry-brief.md");
    const box = loose.querySelector("input")!;
    expect(box).not.toBeChecked();
    expect(box).not.toBeDisabled();
    expect(loose.textContent).toContain("already uploaded in 'brief.md'");
    expect(screen.getByTestId("dialog-reference-held-notes/").textContent).toBe("1 of 2 already uploaded in 'brief.md'");
    expect(screen.getByTestId("dialog-reference-selection-total").textContent).toContain("Sending 3 files");
    fireEvent.click(box);
    expect(screen.getByTestId("dialog-reference-selection-total").textContent).toContain("Sending 4 files");
  });

  it("SelectionCard_FolderEntirelyHeld_StartsUnticked", async () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} loadHeld={async () => held} />);

    fireEvent.change(screen.getByTestId("dialog-composer-folder"), { target: { files: [
      folderFile("# brief", "v2/notes/brief.md"), folderFile("new", "v2/new.md"),
    ] } });

    const folder = await screen.findByTestId("dialog-reference-entry-notes/");
    expect(folder.querySelector("input")).not.toBeChecked();
    expect(folder.textContent).toContain("1 of 1 already uploaded in 'brief.md'");
  });
});
