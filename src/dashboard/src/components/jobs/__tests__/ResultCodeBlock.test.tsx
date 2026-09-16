import { render, screen } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { ResultCodeBlock } from "../ResultCodeBlock";

describe("ResultCodeBlock", () => {
  it("LogOutput_UsesTerminalPanelSurface", () => {
    render(<ResultCodeBlock className="language-bash">echo hi</ResultCodeBlock>);
    expect(screen.getByTestId("result-code-block").className).toContain("card-terminal-panel");
  });

  it("InlineCode_StaysInline_NotTerminalPanel", () => {
    render(<ResultCodeBlock inline>x</ResultCodeBlock>);
    expect(screen.queryByTestId("result-code-block")).not.toBeInTheDocument();
  });

  // 2026-09-15-cb3e: react-markdown stopped passing `inline`, so the shape of the node has
  // to decide — otherwise a session id in backticks renders as a terminal panel inside the
  // paragraph around it.
  it("ResultCodeBlock_WithoutTheInlineFlag_ReadsTheShapeOfTheNode", () => {
    const view = render(<ResultCodeBlock>s-1</ResultCodeBlock>);
    expect(screen.queryByTestId("result-code-block")).not.toBeInTheDocument();
    view.unmount();

    render(<ResultCodeBlock>{"SELECT 1;\nSELECT 2;"}</ResultCodeBlock>);
    expect(screen.getByTestId("result-code-block")).toBeInTheDocument();
  });
});
