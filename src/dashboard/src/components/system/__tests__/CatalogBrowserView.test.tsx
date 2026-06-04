import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CatalogBrowserView } from "../CatalogBrowserView";
import type { CatalogContents } from "@/lib/catalogApi";

const contents: CatalogContents = {
  ready: true,
  masters: [{ name: "coding-agent-master", role: "master", description: "Drives agentic code edits" }],
  skills: [{ name: "auth-reviewer", role: "investigator", description: "Finds broken authz" }],
  concepts: [
    { name: "authentication", type: "Bool", description: "Whether the API authenticates requests" },
    { name: "rate_limit", type: "Int", description: "Requests allowed per window" },
  ],
};

describe("CatalogBrowserView", () => {
  it("CatalogBrowser_ExpandMaster_RendersMarkdownBody", async () => {
    const loadBody = (name: string) => Promise.resolve(`# ${name}\n\nThe coding master rules.`);
    render(<CatalogBrowserView contents={contents} loadBody={loadBody} />);

    // Body is lazy — not present until the card is expanded.
    expect(screen.queryByText("The coding master rules.")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("catalog-entry-toggle-coding-agent-master"));

    expect(await screen.findByText("The coding master rules.")).toBeInTheDocument();
  });

  it("CatalogBrowser_Concepts_ShowTypeAndDefinition_Filterable", () => {
    render(<CatalogBrowserView contents={contents} loadBody={() => Promise.resolve(null)} />);

    const auth = screen.getByTestId("catalog-concept-authentication");
    expect(auth).toHaveTextContent("authentication");
    expect(auth).toHaveTextContent("bool");
    expect(auth).toHaveTextContent("Whether the API authenticates requests");

    fireEvent.change(screen.getByTestId("catalog-concept-filter"), { target: { value: "auth" } });

    expect(screen.getByTestId("catalog-concept-authentication")).toBeInTheDocument();
    expect(screen.queryByTestId("catalog-concept-rate_limit")).not.toBeInTheDocument();
  });
});
