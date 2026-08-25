import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { execFileSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

// 2026-08-25-21ae: the entrypoint is the half of this feature that no TypeScript
// test could reach — it is what turns an operator's environment into the
// document the client above reads. The repo has no shell-test harness, so it is
// run directly: the script writes to the path its own variable names, which is
// what lets a case assert the contract without building an image.

// Off the vitest root rather than import.meta.url: the jsdom environment hands
// a module an http: URL, which is not a path.
const ENTRYPOINT = resolve(process.cwd(), "docker-entrypoint.sh");

let workspace: string;
let settingsFile: string;

beforeEach(() => {
  workspace = mkdtempSync(join(tmpdir(), "agentsmith-entrypoint-"));
  settingsFile = join(workspace, "public", "runtime-settings.json");
});

afterEach(() => {
  rmSync(workspace, { recursive: true, force: true });
});

/** Run the entrypoint with the given environment, then read what it wrote. */
function run(env: Record<string, string>, command: string[] = ["true"]): string {
  return execFileSync("sh", [ENTRYPOINT, ...command], {
    env: { ...process.env, AGENTSMITH_RUNTIME_SETTINGS_FILE: settingsFile, ...env },
    encoding: "utf8",
  });
}

function documentWritten(): Record<string, Record<string, string>> {
  return JSON.parse(readFileSync(settingsFile, "utf8")) as Record<string, Record<string, string>>;
}

describe("the dashboard image entrypoint", () => {
  it("Entrypoint_TheImageCopiesIt_TheScriptIsWhereTheDockerfileLooks", () => {
    expect(existsSync(ENTRYPOINT)).toBe(true);
  });

  it("Entrypoint_NoVariablesSet_WritesADocumentWithEmptyValues", () => {
    run({
      AGENTSMITH_AUTH_AUTHORITY: "",
      AGENTSMITH_AUTH_CLIENT_ID: "",
      AGENTSMITH_AUTH_AUDIENCE: "",
      AGENTSMITH_AUTH_SCOPES: "",
      AGENTSMITH_AUTH_REDIRECT_PATH: "",
    });

    // Every value an installation identifies itself with is empty — nothing is
    // configured, which is what an installation that sets nothing already does.
    // The redirect path names a route inside the dashboard rather than anything
    // about the installation, so it carries the same built-in default the client
    // resolves when there is no document at all.
    expect(documentWritten()).toEqual({
      auth: {
        authority: "",
        clientId: "",
        audience: "",
        scopes: "",
        redirectPath: "/signin-callback",
      },
    });
  });

  it("Entrypoint_VariablesSet_WritesThemVerbatim", () => {
    run({
      AGENTSMITH_AUTH_AUTHORITY: "https://login.example.com/realms/agentsmith",
      AGENTSMITH_AUTH_CLIENT_ID: "agentsmith-dashboard",
      AGENTSMITH_AUTH_AUDIENCE: "agent-smith",
      AGENTSMITH_AUTH_SCOPES: "openid profile",
      AGENTSMITH_AUTH_REDIRECT_PATH: "/callback",
    });

    expect(documentWritten()).toEqual({
      auth: {
        authority: "https://login.example.com/realms/agentsmith",
        clientId: "agentsmith-dashboard",
        audience: "agent-smith",
        scopes: "openid profile",
        redirectPath: "/callback",
      },
    });
  });

  it("Entrypoint_ValueCarriesAQuote_TheDocumentStaysReadable", () => {
    // A quote or a backslash written straight into the file ends the JSON string
    // early, and the browser then reads nothing at all rather than one bad field.
    run({ AGENTSMITH_AUTH_CLIENT_ID: 'a"b\\c' });

    expect(documentWritten().auth.clientId).toBe('a"b\\c');
  });

  it("Entrypoint_AfterWriting_ExecsTheServer", () => {
    // The command's own output proves it ran, and that it could already see the
    // document — the ordering the whole mechanism depends on.
    const output = run({ AGENTSMITH_AUTH_CLIENT_ID: "agentsmith-dashboard" }, [
      "sh",
      "-c",
      'printf "SERVER STARTED " && cat "$AGENTSMITH_RUNTIME_SETTINGS_FILE"',
    ]);

    expect(output).toContain("SERVER STARTED");
    expect(output).toContain("agentsmith-dashboard");
  });
});
