import { render, screen, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";

let mockResult: { content: string | null; loading: boolean; error: string | null };

vi.mock("@/hooks/useResultMarkdown", () => ({
  useResultMarkdown: () => mockResult,
}));

import { ResultTab } from "../ResultTab";

beforeEach(() => {
  mockResult = { content: null, loading: false, error: null };
});

describe("ResultTab", () => {
  it("renders headings, tables, code-fences from markdown content", async () => {
    mockResult = {
      content: `# Run Result\n\n## Cost breakdown\n\n| Skill | Tokens | USD |\n| --- | --- | --- |\n| plan | 1k | 0.01 |\n\n\`\`\`json\n{ "ok": true }\n\`\`\`\n`,
      loading: false,
      error: null,
    };
    render(<ResultTab runId="r" prUrl={null} />);
    await waitFor(() => {
      expect(screen.getByRole("heading", { level: 1, name: "Run Result" })).toBeInTheDocument();
    });
    expect(screen.getByRole("heading", { level: 2, name: "Cost breakdown" })).toBeInTheDocument();
    expect(screen.getByRole("table")).toBeInTheDocument();
    const codeBlocks = screen.getAllByTestId("result-code-block");
    expect(codeBlocks.length).toBeGreaterThan(0);
  });

  it("missing content WITH prUrl renders View-in-PR link", () => {
    mockResult = { content: null, loading: false, error: null };
    render(<ResultTab runId="r" prUrl="https://example.com/pr/1" />);
    const link = screen.getByTestId("result-pr-link");
    expect(link).toHaveAttribute("href", "https://example.com/pr/1");
  });

  it("missing content WITHOUT prUrl renders completes-on-finish message", () => {
    mockResult = { content: null, loading: false, error: null };
    render(<ResultTab runId="r" prUrl={null} />);
    expect(screen.queryByTestId("result-pr-link")).not.toBeInTheDocument();
    expect(screen.getByText(/result becomes visible when the run finishes/i)).toBeInTheDocument();
  });

  it("loading state shows spinner-equivalent", () => {
    mockResult = { content: null, loading: true, error: null };
    render(<ResultTab runId="r" prUrl={null} />);
    expect(screen.getByTestId("result-loading")).toBeInTheDocument();
  });

  it("error state shows the message", () => {
    mockResult = { content: null, loading: false, error: "boom" };
    render(<ResultTab runId="r" prUrl={null} />);
    expect(screen.getByTestId("result-error")).toHaveTextContent(/boom/);
  });
});
