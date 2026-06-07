import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import { AnalyzeMarkdownSection } from "../AnalyzeMarkdownSection";

// p0247: the shared analyze.md panel — rendered on the Architecture node AND in
// the Analyze-codebase step's detail footer. Drive it off a mocked hook.
let mockContent: string | null = null;
vi.mock("@/hooks/useAnalyzeMarkdown", () => ({
  useAnalyzeMarkdown: () => ({ content: mockContent, loading: false, error: null }),
}));

describe("AnalyzeMarkdownSection", () => {
  it("RendersAnalyzeMarkdown_WhenPresent", () => {
    mockContent = "# Project map\n\nLanguage: C#";
    render(<AnalyzeMarkdownSection runId="run-1" />);

    expect(screen.getByTestId("analyze-section")).toBeInTheDocument();
    expect(screen.getByTestId("analyze-markdown")).toHaveTextContent("Language: C#");
  });

  it("RendersNothing_WhenAnalyzeAbsent", () => {
    mockContent = null;
    render(<AnalyzeMarkdownSection runId="run-1" />);

    expect(screen.queryByTestId("analyze-section")).not.toBeInTheDocument();
  });
});
