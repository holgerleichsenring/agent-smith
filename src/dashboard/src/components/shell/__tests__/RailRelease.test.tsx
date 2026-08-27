import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { RailRelease } from "../RailRelease";

// 2026-08-27-729e: one line in the rail footer, beside who is signed in. It reads THIS
// bundle's own stamp, so it needs no request and survives a server that is not answering.

describe("RailRelease", () => {
  it("Rail_TheReleaseLine_LinksToTheInstallationReadOut", () => {
    render(<RailRelease />);

    expect(screen.getByTestId("rail-release-link").getAttribute("href"))
      .toBe("/system/installation");
  });

  it("Rail_AnUnstampedBundle_StillOffersTheReadOut", () => {
    // No NEXT_PUBLIC_RELEASE_VERSION in a test bundle — the line still leads somewhere.
    render(<RailRelease />);

    expect(screen.getByTestId("rail-release").textContent).toBeTruthy();
  });
});
