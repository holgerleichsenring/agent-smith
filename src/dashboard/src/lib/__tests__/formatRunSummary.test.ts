import { describe, it, expect } from "vitest";
import { formatRunSummary } from "@/lib/formatRunSummary";

const usageLimitBody =
  '{"type":"error","error":{"type":"invalid_request_error","message":"You have reached your specified API usage limits. You will regain access on 2026-08-01 at 00:00 UTC."},"request_id":"req_011AbCdEfGh"}';

describe("formatRunSummary", () => {
  it("formatRunSummary_ErrorBody_ExtractsMessage", () => {
    expect(formatRunSummary(usageLimitBody)).toBe(
      "You have reached your specified API usage limits. You will regain access on 2026-08-01 at 00:00 UTC.",
    );
  });

  it("extracts the message from a body embedded in prefixed text", () => {
    expect(formatRunSummary(`Provider call failed: ${usageLimitBody}`)).toBe(
      "You have reached your specified API usage limits. You will regain access on 2026-08-01 at 00:00 UTC.",
    );
  });

  it("passes curated prose through unchanged", () => {
    const curated =
      "Keystone verdict: the contract was not satisfied — the integration test timed out and the expected changelog entry was not found.";
    expect(formatRunSummary(curated)).toBe(curated);
  });

  it("passes prose containing braces through unchanged", () => {
    const prose = "The step failed while rendering {placeholder} in the template.";
    expect(formatRunSummary(prose)).toBe(prose);
  });

  it("passes JSON without error.message through unchanged", () => {
    const json = '{"type":"result","summary":"3 files changed","error":null}';
    expect(formatRunSummary(json)).toBe(json);
  });

  it("passes an empty string through unchanged", () => {
    expect(formatRunSummary("")).toBe("");
  });
});
