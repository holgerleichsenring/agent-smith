import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import type { CatalogContents } from "@/lib/catalogApi";
import type { CriteriaMet } from "@/lib/expectationsApi";
import { CatalogBrowserView } from "../CatalogBrowserView";
import { ExpectationMetricsView } from "../ExpectationMetricsView";

// 2026-11-23-5669e: the System pages in German — the catalog's sections and its read time, and the criteria outcomes'
// counts read from the dictionary; what the server names (the origin phrase, a skill's description) is shown as it came.

const contents: CatalogContents = {
  ready: true,
  origin: { phrase: "embedded v5.2.0 at /var/cache/agentsmith/skills", overlayPath: null, readAt: "2026-09-18T14:30:00.000Z" },
  masters: [{ name: "coding-agent-master", role: "master", description: "Drives agentic code edits" }],
  skills: [],
  concepts: [{ name: "rate_limit", type: "Int", description: "Requests allowed per window" }],
};

const counts = { met: 3, unmet: 1, unproven: 2, notApplicable: 0, judged: 6, share: 0.5, overruled: 1, staleOverrules: 1 };
const criteria = {
  runs: 4,
  counts,
  projects: [{ project: "web", runs: 4, counts, months: [] }],
} as unknown as CriteriaMet;

describe("System in German", () => {
  it("CatalogBrowserView_German_WordsTheSectionsButNotTheCatalog", () => {
    resetLanguage("de");
    render(<CatalogBrowserView contents={contents} loadBody={() => Promise.resolve(null)} />);
    expect(screen.getByText("Master")).toBeInTheDocument();
    expect(screen.getByText("Konzepte")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Konzepte filtern…")).toBeInTheDocument();
    expect(screen.getByTestId("catalog-origin-read-at")).toHaveTextContent(
      `gelesen ${new Date(contents.origin!.readAt).toLocaleString("de-DE")}`,
    );
    expect(screen.getByTestId("catalog-origin-source")).toHaveTextContent("Einstellungen → Skills");
    expect(screen.getByText("Drives agentic code edits")).toBeInTheDocument();
  });

  it("ExpectationMetricsView_German_CountsInGerman", () => {
    resetLanguage("de");
    render(<ExpectationMetricsView data={criteria} error={null} />);
    expect(screen.getByText("Kriterien-Ergebnisse")).toBeInTheDocument();
    expect(screen.getByTestId("exp-metric-share")).toHaveTextContent("3 von 6");
    expect(screen.getByTestId("exp-metric-overruled")).toHaveTextContent("1 veraltet");
    expect(screen.getByTestId("expectations-project-web")).toHaveTextContent(
      "4 Runs · 3 erfüllt von 6 bewertet · 1 unerfüllt · 2 unbelegt · 1 überstimmt · 1 veraltete Überstimmung",
    );
  });
});
