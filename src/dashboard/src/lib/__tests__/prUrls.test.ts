import { describe, it, expect } from "vitest";
import { extractUrls, splitOnUrls } from "../prUrls";

// p0502: CommitAndPRHandler and InitCommitHandler join a run's pull-request URLs
// with ", ". A URL pattern that runs to the next whitespace therefore swallows the
// separating comma into every link but the last, and those links 404.
const AZURE = "https://dev.azure.com/Org/Project/_git/Sample.Service/pullrequest";
const COMMA_JOINED = `Pull requests created: ${AZURE}/9018, ${AZURE}/9019, ${AZURE}/9020`;

const urlsOf = (text: string) => splitOnUrls(text).filter((s) => s.isUrl).map((s) => s.value);
const proseOf = (text: string) => splitOnUrls(text).filter((s) => !s.isUrl).map((s) => s.value);

describe("splitOnUrls", () => {
  it("SplitOnUrls_CommaJoinedList_EndsEachUrlBeforeTheComma", () => {
    expect(urlsOf(COMMA_JOINED)).toEqual([`${AZURE}/9018`, `${AZURE}/9019`, `${AZURE}/9020`]);
  });

  it("SplitOnUrls_TrimmedPunctuation_StaysInTheProse", () => {
    // The separator is the operator's text, not ours to delete — dropping it would
    // silently rewrite the sentence into "…/9018 …/9019".
    expect(proseOf(COMMA_JOINED).join("")).toBe("Pull requests created: , , ");
    expect(splitOnUrls(COMMA_JOINED).map((s) => s.value).join("")).toBe(COMMA_JOINED);
  });

  it("SplitOnUrls_SentenceEndingUrl_DropsTheFullStop", () => {
    const text = `Opened ${AZURE}/9018.`;
    expect(urlsOf(text)).toEqual([`${AZURE}/9018`]);
    expect(splitOnUrls(text).map((s) => s.value).join("")).toBe(text);
  });

  it("SplitOnUrls_BracketedUrl_StopsAtTheBracket", () => {
    const text = `See (${AZURE}/9018) for details`;
    expect(urlsOf(text)).toEqual([`${AZURE}/9018`]);
  });

  it("SplitOnUrls_NoUrl_ReturnsTheTextUntouched", () => {
    expect(splitOnUrls("no links here")).toEqual([{ value: "no links here", isUrl: false }]);
  });

  it("SplitOnUrls_EmptyText_ReturnsNothing", () => {
    expect(splitOnUrls("")).toEqual([]);
  });

  it("SplitOnUrls_AgreesWithExtractUrls_OnTheSameText", () => {
    // One URL vocabulary: the split and the extract must never disagree about
    // where a URL ends, which is the divergence that caused this defect.
    expect(urlsOf(COMMA_JOINED)).toEqual(extractUrls(COMMA_JOINED));
  });
});
