import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { DialogComposer } from "../DialogComposer";

// 2026-10-01-283db: the attach menu's Website and Folder entries. A folder pick hands on every
// file with the path the browser gave it, which is what the upload names each part by.
function folderFile(path: string): File {
  const file = new File(["x"], path.split("/").at(-1)!, { type: "text/css" });
  Object.defineProperty(file, "webkitRelativePath", { value: path });
  return file;
}

describe("DialogComposer, attaching a website", () => {
  it("DialogComposer_TheMenu_OffersWebsiteAndFolderBesideImage", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));

    expect(screen.getAllByRole("menuitem").map((item) => item.textContent)).toEqual(["Image", "Website", "Folder"]);
    expect(screen.getByTestId("dialog-composer-folder")).toHaveAttribute("webkitdirectory");
    expect(screen.getByTestId("dialog-composer-website")).toHaveAttribute("multiple");
    expect(screen.getByTestId("dialog-composer-website").getAttribute("accept")).toContain(".zip");
  });

  it("DialogComposer_Folder_HandsOnEveryFileWithItsRelativePath", () => {
    const onAttachSite = vi.fn();
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} onAttachSite={onAttachSite} />);
    const clicked = vi.fn();
    screen.getByTestId("dialog-composer-folder").addEventListener("click", clicked);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));
    fireEvent.click(screen.getByRole("menuitem", { name: "Folder" }));
    fireEvent.change(screen.getByTestId("dialog-composer-folder"), {
      target: { files: [folderFile("site/index.html"), folderFile("site/css/a.css")] },
    });

    expect(clicked).toHaveBeenCalledTimes(1);
    const [files] = onAttachSite.mock.calls[0] as [File[]];
    expect(files.map((file) => file.webkitRelativePath)).toEqual(["site/index.html", "site/css/a.css"]);
  });

  it("DialogComposer_WithoutASiteHandler_OffersOnlyImage", () => {
    render(<DialogComposer onSend={vi.fn()} onAttach={vi.fn()} />);

    fireEvent.click(screen.getByRole("button", { name: "Attach" }));

    expect(screen.getAllByRole("menuitem").map((item) => item.textContent)).toEqual(["Image"]);
  });
});
