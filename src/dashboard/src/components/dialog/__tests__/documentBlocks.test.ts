import { describe, it, expect } from "vitest";
import { documentBlocks, documentTitle } from "../documentBlocks";

// 2026-10-01-aeb6b: a reply is split into prose and documents before markdown sees it.
describe("documentBlocks", () => {
  it("splits prose and four-backtick document fences, keeping inner code fences", () => {
    const reply = "Here it is.\n\n````document\n# Hand-off\n```yaml\nphase: p1\n```\nGo.\n````\n\nAsk me more.";

    expect(documentBlocks(reply)).toEqual([
      { kind: "prose", text: "Here it is." },
      { kind: "document", text: "# Hand-off\n```yaml\nphase: p1\n```\nGo." },
      { kind: "prose", text: "Ask me more." },
    ]);
  });

  it("treats an unclosed fence as a document to the end", () => {
    expect(documentBlocks("Start.\n````document\n# Cut\npartial line")).toEqual([
      { kind: "prose", text: "Start." },
      { kind: "document", text: "# Cut\npartial line" },
    ]);
  });

  it("leaves a reply without a document as one prose part", () => {
    expect(documentBlocks("Plain ```sql\nSELECT 1;\n``` answer.")).toEqual([
      { kind: "prose", text: "Plain ```sql\nSELECT 1;\n``` answer." },
    ]);
  });

  it("names a document by its first heading", () => {
    expect(documentTitle("intro\n## Project state\nbody")).toBe("Project state");
    expect(documentTitle("no heading")).toBeNull();
  });
});
