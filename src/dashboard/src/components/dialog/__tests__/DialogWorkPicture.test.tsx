import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { DialogWorkPicture } from "../DialogWorkPicture";

// 2026-11-05-862ea: the stored work picture is shown on one line under the heading.
describe("DialogWorkPicture", () => {
  it("shows kind, size and truth on one line", () => {
    render(
      <DialogWorkPicture
        picture={{ kind: "rebuild", truth: "the handover upload; repo-a is scaffolding", size: "oneSpec" }}
      />,
    );

    expect(screen.getByTestId("dialog-work-picture").textContent).toBe(
      "Work picture: rebuild · one spec · the handover upload; repo-a is scaffolding",
    );
  });
});
