import { describe, it, expect } from "vitest";
import { useState } from "react";
import { render, screen, fireEvent } from "@testing-library/react";
import { EntityForm } from "../EntityForm";
import type { ConfigCatalog } from "../useConfigCatalog";
import type { StudioProject } from "@/lib/configApi";

const catalog: ConfigCatalog = {
  agents: [{ id: "gpt5", provider: "openai", models: { coding: "c", scan: "s" }, keySecret: "K" }],
  trackers: [{ id: "azdo", type: "azure", org: "o", project: "p", authSecret: "T" }],
  repos: [
    { id: "web", name: "web", branch: "main" },
    { id: "api", name: "api", branch: "main" },
  ],
  projects: [],
  "mcp-servers": [],
  secrets: [{ id: "K" }, { id: "T" }],
};

function Harness() {
  const [draft, setDraft] = useState<StudioProject>({
    id: "proj",
    agent: "",
    tracker: "",
    repos: [],
    trigger: "",
    pipelines: [],
  });
  return (
    <EntityForm
      kind="projects"
      draft={draft}
      onChange={(n) => setDraft(n as StudioProject)}
      catalog={catalog}
      isNew
    />
  );
}

describe("ProjectForm", () => {
  it("ProjectForm_RefsPickedFromCatalog_NeverFreeText", () => {
    render(<Harness />);
    // agent + tracker refs are <select>s (pick-only), never text inputs.
    const agent = screen.getByTestId("form-ref-agent");
    const tracker = screen.getByTestId("form-ref-tracker");
    expect(agent.tagName).toBe("SELECT");
    expect(tracker.tagName).toBe("SELECT");
    // The options come straight from the catalog.
    expect(agent.querySelector('option[value="gpt5"]')).not.toBeNull();
    expect(tracker.querySelector('option[value="azdo"]')).not.toBeNull();
    // Repos are pick-only toggle chips, one per catalog repo — no text entry.
    expect(screen.getByTestId("form-ref-repos-option-web")).toBeInTheDocument();
    expect(screen.getByTestId("form-ref-repos-option-api")).toBeInTheDocument();
  });

  it("ProjectForm_IntegrityFlipsGreen_WhenAllRefsResolve", () => {
    render(<Harness />);
    // Starts unresolved (no refs picked yet).
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");

    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    // Still amber until at least one repo is chosen.
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");

    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));

    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    expect(screen.getByTestId("project-integrity")).toHaveTextContent("integrity confirmed");
  });

  it("ProjectForm_UnknownRepoRemoved_IntegrityReturnsAmber", () => {
    render(<Harness />);
    fireEvent.change(screen.getByTestId("form-ref-agent"), { target: { value: "gpt5" } });
    fireEvent.change(screen.getByTestId("form-ref-tracker"), { target: { value: "azdo" } });
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "true");
    // Deselecting the only repo drops integrity back to amber.
    fireEvent.click(screen.getByTestId("form-ref-repos-option-web"));
    expect(screen.getByTestId("project-integrity")).toHaveAttribute("data-ok", "false");
  });
});
