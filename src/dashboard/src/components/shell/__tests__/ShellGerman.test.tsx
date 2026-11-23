import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { translator } from "@/lib/i18n/translate";
import { waitingSentence } from "@/components/home/homeItems";
import { FailedSurface } from "../FailedSurface";
import { RefusalSurface } from "../RefusalSurface";
import { placeForQuery, searchablePlaces } from "../AppTopBar";

// 2026-11-23-5669a: the shell in German — the failure and refusal panels, the page search and home's waiting line.

describe("the shell in German", () => {
  it("FailedSurface_German_NamesTheSurfaceInGerman", () => {
    resetLanguage("de");
    render(<FailedSurface surface="Ticketliste" error={new Error("boom")} onRetry={() => {}} />);
    expect(screen.getByTestId("failed-surface")).toHaveTextContent("„Ticketliste“ ließ sich nicht darstellen.");
    expect(screen.getByTestId("failed-surface-retry")).toHaveTextContent("Erneut versuchen");
  });

  it("RefusalSurface_German_WordsAMissingPermission", () => {
    resetLanguage("de");
    render(
      <RefusalSurface
        refusal={{ kind: "permission", missingPermissions: ["runs.read"], path: "/api/runs" }}
        surface="die Pull Requests"
      />,
    );
    expect(screen.getByTestId("refusal-surface")).toHaveTextContent("Du bist angemeldet, darfst die Pull Requests aber nicht sehen.");
    expect(screen.getByTestId("refusal-surface")).toHaveTextContent("Der Server nennt die Berechtigung, die er verlangt und die dir fehlt:");
  });

  it("PlaceForQuery_German_FindsAPageByItsGermanAndItsEnglishName", () => {
    const places = searchablePlaces(translator("de"));
    expect(placeForQuery("übersicht", places)).toBe("/overview");
    expect(placeForQuery("Overview", places)).toBe("/overview");
  });

  // 2026-11-22-5485b: the config catalog and settings pages are named in German too.
  it("PlaceForQuery_German_FindsConfigCatalogAndSettingsPages", () => {
    const places = searchablePlaces(translator("de"));
    expect(placeForQuery("Verbindungen", places)).toBe("/config/connections");
    expect(placeForQuery("Warteschlange", places)).toBe("/config/settings/queue");
  });

  it("WaitingSentence_German_CountsInGerman", () => {
    const de = translator("de");
    expect(waitingSentence(de, 0)).toBe("Nichts wartet auf dich.");
    expect(waitingSentence(de, 1)).toBe("Eine Sache wartet auf dich.");
    expect(waitingSentence(de, 4)).toBe("4 Sachen warten auf dich.");
  });
});
