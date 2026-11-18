import { describe, expect, it } from "vitest";
import { isPathLike, shortPath, shortTargets, splitList } from "../logPaths";

// 2026-11-18-e06ba: a path keeps where it is (first segment) and what it is (last), the middle elided.

describe("logPaths", () => {
  it("shortPath_DeepPath_KeepsRootAndFile", () => {
    expect(shortPath("Shop.Server/Shop.Infrastructure.Persistence/Shop.Infrastructure.Persistence.csproj"))
      .toBe("Shop.Server/…/Shop.Infrastructure.Persistence.csproj");
  });

  it("shortPath_TwoSegmentsOrNoPath_StaysAsItIs", () => {
    expect(shortPath("shop/package.json")).toBe("shop/package.json");
    expect(shortPath("Shop.Server")).toBe("Shop.Server");
    expect(shortPath("dotnet test api/Api.Tests/Api.Tests.csproj")).toBe("dotnet test api/Api.Tests/Api.Tests.csproj");
  });

  it("isPathLike_SlashWithoutWhitespace", () => {
    expect(isPathLike("a/b")).toBe(true);
    expect(isPathLike("a b/c")).toBe(false);
    expect(isPathLike("abc")).toBe(false);
  });

  it("shortTargets_SplitsTheListAndShortensEach", () => {
    expect(splitList("a; b;; c ")).toEqual(["a", "b", "c"]);
    expect(shortTargets("Shop.Server; api/src/x/Program.cs; Shop.Client")).toBe("Shop.Server, api/…/Program.cs, Shop.Client");
  });
});
