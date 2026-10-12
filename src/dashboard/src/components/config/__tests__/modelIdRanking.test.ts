import { describe, it, expect } from "vitest";
import type { ListedModelPrice } from "@/lib/configApi";
import { preferredProvidersFor, rankModelMatches } from "../agent/modelIdRanking";

// 2026-10-12-6bfae: the picker's order — fitting provider, then where the query matches.

const listed = (id: string, provider: string): ListedModelPrice =>
  ({ id, provider, inputPerMillion: 1, outputPerMillion: 1, cacheReadPerMillion: null, contextWindowTokens: null });

const LIST = [
  listed("anthropic.claude-sonnet-5-5", "bedrock_converse"),
  listed("bedrock/us-gov-east-1/anthropic.claude-sonnet-5-5", "bedrock"),
  listed("claude-sonnet-5-5", "anthropic"),
  listed("claude-sonnet-5-5-20260101", "anthropic"),
  listed("xsonnet-5-5", "other"),
  listed("sonnet-5-5-mini", "other"),
];

const ids = (ms: ListedModelPrice[]) => ms.map((m) => m.id);

describe("rankModelMatches", () => {
  it("SonnetOnAnthropic_OffersClaudeSonnetFirst", () => {
    expect(ids(rankModelMatches(LIST, "sonnet-5-5", ["anthropic"], 3))).toEqual([
      "claude-sonnet-5-5",
      "claude-sonnet-5-5-20260101",
      "sonnet-5-5-mini",
    ]);
  });

  it("NoPreferredProvider_RanksPrefixThenBoundaryThenSubstring", () => {
    expect(ids(rankModelMatches(LIST, "sonnet-5-5", [], 10))).toEqual([
      "sonnet-5-5-mini",
      "claude-sonnet-5-5",
      "claude-sonnet-5-5-20260101",
      "anthropic.claude-sonnet-5-5",
      "bedrock/us-gov-east-1/anthropic.claude-sonnet-5-5",
      "xsonnet-5-5",
    ]);
  });

  it("OtherProviders_StillOffered_AfterTheFittingOnes", () => {
    const ranked = ids(rankModelMatches(LIST, "claude-sonnet", ["anthropic"], 10));
    expect(ranked.slice(0, 2)).toEqual(["claude-sonnet-5-5", "claude-sonnet-5-5-20260101"]);
    expect(ranked).toContain("anthropic.claude-sonnet-5-5");
  });

  it("ExactId_IsLeftOut", () => {
    expect(ids(rankModelMatches(LIST, "Claude-Sonnet-5-5", ["anthropic"], 10))).not.toContain("claude-sonnet-5-5");
  });
});

describe("preferredProvidersFor", () => {
  it("LooksUpTheTypeCaseInsensitively", () => {
    expect(preferredProvidersFor({ anthropic: ["anthropic"] }, "Anthropic")).toEqual(["anthropic"]);
  });

  it("UnknownTypeOrOlderServer_PrefersNothing", () => {
    expect(preferredProvidersFor({ anthropic: ["anthropic"] }, "ollama")).toEqual([]);
    expect(preferredProvidersFor(undefined, "anthropic")).toEqual([]);
    expect(preferredProvidersFor({ anthropic: ["anthropic"] }, null)).toEqual([]);
  });
});
