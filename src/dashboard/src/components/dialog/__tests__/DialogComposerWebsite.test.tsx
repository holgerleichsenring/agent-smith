import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogComposer } from "../DialogComposer";

// 2026-10-01-283db: the attach menu's Files (once Website) and Folder entries. A folder pick hands
// on every file with the path the browser gave it, which is what the upload names each part by.
function folderFile(path: string): File {
  const file = new File(["x"], path.split("/").at(-1)!, { type: "text/css" });
  Object.defineProperty(file, "webkitRelativePath", { value: path });
  return file;
}

describe("DialogComposer, attaching files", () => {
  // 2026-10-08-e8b9i: the browser adds its own prompt to a folder pick; the menu says so before,
  // in a line of its own that describes the item without renaming it.
  it("DialogComposer_FolderEntry_NamedFolderDescribedByTheBrowserHint", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Attach" }));

    const folder = screen.getByRole("menuitem", { name: "Folder" });
    expect(folder).toHaveAccessibleDescription("Your browser may ask to confirm a folder upload.");
    expect(screen.getByTestId("dialog-composer-folder-hint")).toHaveClass("d-menu-sub");
  });

  // 2026-10-02-075db: 'Website' became 'Files', and it takes any type — the server keeps any
  // authored file since 075da, so the dialog no longer narrows the pick to website types.
  it("DialogComposer_TheMenu_OffersFilesAndFolderBesideImage", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));

    expect(screen.getAllByRole("menuitem").map((item) => item.textContent)).toEqual(["Image", "Files", "Folder"]);
    expect(screen.getByTestId("dialog-composer-folder")).toHaveAttribute("webkitdirectory");
    expect(screen.getByTestId("dialog-composer-files")).toHaveAttribute("multiple");
    expect(screen.getByTestId("dialog-composer-files")).not.toHaveAttribute("accept");
  });

  // 2026-10-02-075db: a folder pick is shown before it is sent; Send hands on the ticked files,
  // each with the path the browser gave it, and what the card left out.
  it("DialogComposer_AFolderPick_ShowsTheCardAndSendsOnlyTickedFiles", async () => {
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={onAttachSite} />);
    const clicked = vi.fn();
    screen.getByTestId("dialog-composer-folder").addEventListener("click", clicked);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Folder" }));
    fireEvent.change(screen.getByTestId("dialog-composer-folder"), {
      target: { files: [folderFile("site/index.html"), folderFile("site/css/a.css"), folderFile("site/.venv/lib/x.py")] },
    });

    expect(clicked).toHaveBeenCalledTimes(1);
    const venv = await screen.findByTestId("dialog-reference-entry-.venv/");
    expect(within(venv).getByRole("checkbox")).not.toBeChecked();
    expect(venv).toHaveTextContent("rebuildable");
    expect(onAttachSite).not.toHaveBeenCalled();

    fireEvent.click(within(screen.getByTestId("dialog-reference-entry-css/")).getByRole("checkbox"));
    fireEvent.click(screen.getByTestId("dialog-reference-selection-send"));

    const [files, leftOut] = onAttachSite.mock.calls[0] as [File[], { count: number; summary: string[] }];
    expect(files.map((file) => file.webkitRelativePath)).toEqual(["site/index.html"]);
    expect(leftOut).toEqual({ count: 2, summary: ["css/ (unticked)", ".venv/ (rebuildable)"] });
    expect(screen.queryByTestId("dialog-reference-selection")).not.toBeInTheDocument();
  });

  it("DialogComposer_CancelOnTheCard_UploadsNothing", async () => {
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={onAttachSite} />);

    fireEvent.change(screen.getByTestId("dialog-composer-files"), {
      target: { files: [new File(["a"], "a.html"), new File(["b"], "b.css")] },
    });
    fireEvent.click(await screen.findByTestId("dialog-reference-selection-cancel"));

    expect(onAttachSite).not.toHaveBeenCalled();
    expect(screen.queryByTestId("dialog-reference-selection")).not.toBeInTheDocument();
  });

  it("DialogComposer_ASingleZip_UploadsAtOnce", () => {
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={onAttachSite} />);

    fireEvent.change(screen.getByTestId("dialog-composer-files"), {
      target: { files: [new File(["PK"], "site.zip", { type: "application/zip" })] },
    });

    expect(onAttachSite).toHaveBeenCalledTimes(1);
    expect((onAttachSite.mock.calls[0][0] as File[])[0].name).toBe("site.zip");
    expect(screen.queryByTestId("dialog-reference-selection")).not.toBeInTheDocument();
  });

  it("DialogComposer_ASelectionOverTheBound_DisablesSendSayingByHowMuch", async () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} />);
    const big = (name: string) => {
      const file = new File(["x"], name);
      Object.defineProperty(file, "size", { value: 15 * 1024 * 1024 });
      return file;
    };

    fireEvent.change(screen.getByTestId("dialog-composer-files"), { target: { files: [big("a.mp4"), big("b.mp4")] } });

    expect(await screen.findByTestId("dialog-reference-selection-total"))
      .toHaveTextContent("Sending 2 files, 30 MB — 5 MB over the 25 MB a set may hold.");
    expect(screen.getByTestId("dialog-reference-selection-send")).toBeDisabled();
  });

  it("DialogComposer_WithoutASiteHandler_OffersOnlyImage", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));

    expect(screen.getAllByRole("menuitem").map((item) => item.textContent)).toEqual(["Image"]);
  });
});

// 2026-10-02-0d72: the composer says what the last upload left, under itself.
describe("DialogComposer, the upload note", () => {
  it("DialogComposer_ARefusalNote_IsAnAlertInTheBadTone", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} note={{ tone: "refused", text: "no site file" }} />);

    const note = screen.getByTestId("dialog-composer-upload-note");
    expect(note).toHaveAttribute("role", "alert");
    expect(note).toHaveClass("d-upload-note", "bad");
    expect(note).toHaveTextContent("no site file");
  });

  it("DialogComposer_NoNote_RendersNone", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} />);

    expect(screen.queryByTestId("dialog-composer-upload-note")).not.toBeInTheDocument();
  });
});
