import { describe, expect, it } from "vitest";
import { INSTALLATION, isInstallation, pickable, projectLabel } from "../installation";

describe("installation label", () => {
  it("shows the installation as Agent Smith and every project under its own name", () => {
    expect(projectLabel(INSTALLATION)).toBe("Agent Smith");
    expect(projectLabel("portal")).toBe("portal");
    expect(isInstallation("portal")).toBe(false);
  });

  it("never lets a default pick land on the installation", () => {
    expect(pickable(["portal", INSTALLATION])).toEqual(["portal"]);
    expect(pickable([{ name: INSTALLATION }])).toEqual([]);
  });
});
