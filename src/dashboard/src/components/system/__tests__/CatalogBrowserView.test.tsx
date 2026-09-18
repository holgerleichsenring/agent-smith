import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { CatalogBrowserView } from "../CatalogBrowserView";
import type { CatalogContents } from "@/lib/catalogApi";

const READ_AT = "2026-09-18T14:30:00.000Z";

const contents: CatalogContents = {
  ready: true,
  origin: {
    phrase: "embedded v5.2.0 at /var/cache/agentsmith/skills",
    overlayPath: null,
    readAt: READ_AT,
  },
  masters: [{ name: "coding-agent-master", role: "master", description: "Drives agentic code edits" }],
  skills: [{ name: "auth-reviewer", role: "investigator", description: "Finds broken authz" }],
  concepts: [
    { name: "authentication", type: "Bool", description: "Whether the API authenticates requests" },
    { name: "rate_limit", type: "Int", description: "Requests allowed per window" },
  ],
};

// An installation that lays its own file over the catalog: the phrase carries the
// fingerprint and the synthesised union root, the configured directory rides beside it.
const overlaid: CatalogContents = {
  ...contents,
  origin: {
    phrase: "embedded v5.2.0 + overlay 4f2a9c10b7e3 at /var/cache/agentsmith/skills-overlay",
    overlayPath: "/etc/agentsmith/skills-overlay",
    readAt: READ_AT,
  },
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

  // 2026-09-18-84be: the page renders skill text, so it has to name the binding that text
  // came from — the server's own phrase.
  it("CatalogBrowser_ResolvedOrigin_IsRendered", () => {
    render(<CatalogBrowserView contents={contents} loadBody={() => Promise.resolve(null)} />);

    expect(screen.getByTestId("catalog-origin-phrase")).toHaveTextContent(
      "embedded v5.2.0 at /var/cache/agentsmith/skills",
    );
  });

  // The contents are cached, so the line says WHEN they were read rather than implying now.
  it("CatalogBrowser_WhenTheCatalogWasRead_IsRendered", () => {
    render(<CatalogBrowserView contents={contents} loadBody={() => Promise.resolve(null)} />);

    expect(screen.getByTestId("catalog-origin-read-at")).toHaveTextContent(
      new Date(READ_AT).toLocaleString(),
    );
  });

  // The source is a settings singleton in this dashboard. A page that sent the operator
  // off to cut a release and rebuild would be the confident wrong answer.
  it("CatalogBrowser_WhereTheSourceChanges_NamesTheSkillsSetting", () => {
    render(<CatalogBrowserView contents={contents} loadBody={() => Promise.resolve(null)} />);

    expect(screen.getByTestId("catalog-origin-source")).toHaveTextContent("Settings → Skills");
  });

  // On an overlaid installation the rendered body may be the operator's own file, which
  // the Skills settings form does not carry — so that instruction must not be repeated
  // here, and the directory the operator actually configured is named instead.
  it("CatalogBrowser_OverlaidInstallation_NamesTheOverlayPathAndNotTheSettingsForm", async () => {
    render(
      <CatalogBrowserView
        contents={overlaid}
        loadBody={(name) => Promise.resolve(`# ${name}`)}
      />,
    );

    const line = screen.getByTestId("catalog-origin-source");
    expect(line).toHaveTextContent("/etc/agentsmith/skills-overlay");
    expect(line).not.toHaveTextContent("Settings → Skills");

    fireEvent.click(screen.getByTestId("catalog-entry-toggle-auth-reviewer"));
    const note = await screen.findByTestId("catalog-entry-source-auth-reviewer");
    expect(note).toHaveTextContent("/etc/agentsmith/skills-overlay");
    expect(note).not.toHaveTextContent("Settings → Skills");
  });

  // The expand control used to wear the config studio's edit-affordance class on a page
  // that edits nothing.
  it("CatalogBrowser_ExpandControl_CarriesNoEditAffordanceClass", () => {
    const { container } = render(
      <CatalogBrowserView contents={contents} loadBody={() => Promise.resolve(null)} />,
    );

    expect(container.querySelector(".edit-hint")).toBeNull();
    expect(container.querySelector(".ec-open")).not.toBeNull();
  });
});
