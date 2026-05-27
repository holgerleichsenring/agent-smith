import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { ExpandCollapseControls } from "../ExpandCollapseControls";

describe("ExpandCollapseControls", () => {
  it("invokes expandAll when none expanded", () => {
    const onExpand = vi.fn();
    const onCollapse = vi.fn();
    render(
      <ExpandCollapseControls
        repoNames={["a", "b", "c"]}
        expanded={new Set()}
        onExpandAll={onExpand}
        onCollapseAll={onCollapse}
      />,
    );
    fireEvent.click(screen.getByTestId("expand-all"));
    expect(onExpand).toHaveBeenCalledTimes(1);
    expect(onCollapse).not.toHaveBeenCalled();
  });

  it("disables expandAll when all repos already expanded", () => {
    render(
      <ExpandCollapseControls
        repoNames={["a", "b"]}
        expanded={new Set(["a", "b"])}
        onExpandAll={() => {}}
        onCollapseAll={() => {}}
      />,
    );
    expect(screen.getByTestId("expand-all")).toBeDisabled();
    expect(screen.getByTestId("collapse-all")).not.toBeDisabled();
  });

  it("renders nothing when no repos", () => {
    const { container } = render(
      <ExpandCollapseControls
        repoNames={[]}
        expanded={new Set()}
        onExpandAll={() => {}}
        onCollapseAll={() => {}}
      />,
    );
    expect(container.firstChild).toBeNull();
  });
});
