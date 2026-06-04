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
});
