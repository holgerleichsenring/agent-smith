import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { execFileSync } from "node:child_process";
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import nextConfig from "../../../../next.config";

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
let routesManifest: string;

interface Rewrite {
  source: string;
  destination: string;
  regex?: string;
}

/** The two proxy rules as `next build` writes them, with whatever target the build saw. */
function builtRewrites(target = "http://localhost:8081"): Rewrite[] {
  return [
    { source: "/hub/:path*", destination: `${target}/hub/:path*`, regex: "^/hub(?:/(.*))?$" },
    { source: "/api/:path*", destination: `${target}/api/:path*`, regex: "^/api(?:/(.*))?$" },
  ];
}

function writeManifest(rewrites: unknown): void {
  writeFileSync(routesManifest, JSON.stringify({ version: 3, rewrites, redirects: [] }));
}

beforeEach(() => {
  workspace = mkdtempSync(join(tmpdir(), "agentsmith-entrypoint-"));
  settingsFile = join(workspace, "public", "runtime-settings.json");
  // 2026-11-25-7f25a: the entrypoint also rewrites the server's routes manifest and
  // refuses to start without one, so every case gets the file a build leaves behind.
  routesManifest = join(workspace, "routes-manifest.json");
  writeManifest(builtRewrites());
});

afterEach(() => {
  rmSync(workspace, { recursive: true, force: true });
});

/** Run the entrypoint with the given environment, then read what it wrote. */
function run(env: Record<string, string>, command: string[] = ["true"]): string {
  return execFileSync("sh", [ENTRYPOINT, ...command], {
    env: {
      ...process.env,
      AGENTSMITH_BACKEND_URL: "",
      AGENTSMITH_RUNTIME_SETTINGS_FILE: settingsFile,
      AGENTSMITH_ROUTES_MANIFEST_FILE: routesManifest,
      ...env,
    },
    encoding: "utf8",
    stdio: ["ignore", "pipe", "pipe"],
  });
}

/** Run the entrypoint expecting it to refuse; returns what it printed on stderr. */
function runRefused(env: Record<string, string>): string {
  let started = "";
  try {
    started = run(env, ["sh", "-c", "echo SERVER STARTED"]);
  } catch (error) {
    return String((error as { stderr?: string }).stderr ?? "");
  }
  throw new Error(`the entrypoint started the server: ${started}`);
}

function documentWritten(): Record<string, Record<string, string>> {
  return JSON.parse(readFileSync(settingsFile, "utf8")) as Record<string, Record<string, string>>;
}

function destinations(): Record<string, string> {
  const manifest = JSON.parse(readFileSync(routesManifest, "utf8")) as { rewrites: Rewrite[] };
  return Object.fromEntries(manifest.rewrites.map((rule) => [rule.source, rule.destination]));
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

// 2026-11-25-7f25a: `next build` resolves rewrites() and writes the destinations into
// routes-manifest.json, which the standalone server reads at start; the variable the
// config reads is gone by then. The entrypoint is what makes the container's
// AGENTSMITH_BACKEND_URL the proxy target, WebSocket upgrades on /hub included.
describe("the dashboard image entrypoint's backend target", () => {
  it("Entrypoint_BackendUrlSet_BothProxyRulesPointAtIt", () => {
    run({ AGENTSMITH_BACKEND_URL: "http://server:8081" });

    expect(destinations()).toEqual({
      "/hub/:path*": "http://server:8081/hub/:path*",
      "/api/:path*": "http://server:8081/api/:path*",
    });
  });

  it("Entrypoint_BackendUrlUnset_KeepsTheLocalDevDefault", () => {
    writeManifest(builtRewrites("http://elsewhere:9999"));

    run({});

    expect(destinations()["/api/:path*"]).toBe("http://localhost:8081/api/:path*");
  });

  it("Entrypoint_BackendUrlWithTrailingSlash_WritesNoDoubleSlash", () => {
    run({ AGENTSMITH_BACKEND_URL: "https://agentsmith.example.com/" });

    expect(destinations()["/hub/:path*"]).toBe("https://agentsmith.example.com/hub/:path*");
  });

  it("Entrypoint_RestartWithAnotherValue_MovesTheTarget", () => {
    // A container restart keeps its filesystem: the second start reads the file the
    // first one wrote, so the target must be derived from the source, never patched
    // over the previous destination.
    run({ AGENTSMITH_BACKEND_URL: "http://first:8081" });
    run({ AGENTSMITH_BACKEND_URL: "http://second:8081" });

    expect(destinations()["/api/:path*"]).toBe("http://second:8081/api/:path*");
  });

  it("Entrypoint_RewritesInBeforeAfterFallbackGroups_RewritesThemToo", () => {
    writeManifest({ beforeFiles: [], afterFiles: builtRewrites(), fallback: [] });

    run({ AGENTSMITH_BACKEND_URL: "http://server:8081" });

    const manifest = JSON.parse(readFileSync(routesManifest, "utf8")) as {
      rewrites: { afterFiles: Rewrite[] };
    };
    expect(manifest.rewrites.afterFiles.map((rule) => rule.destination)).toEqual([
      "http://server:8081/hub/:path*",
      "http://server:8081/api/:path*",
    ]);
  });

  it("Entrypoint_RewritingTheTarget_KeepsTheRestOfTheManifest", () => {
    run({ AGENTSMITH_BACKEND_URL: "http://server:8081" });

    const manifest = JSON.parse(readFileSync(routesManifest, "utf8")) as {
      version: number;
      rewrites: Rewrite[];
    };
    expect(manifest.version).toBe(3);
    expect(manifest.rewrites[0].regex).toBe("^/hub(?:/(.*))?$");
  });

  it("Entrypoint_BackendUrlNotHttp_RefusesToStart", () => {
    expect(runRefused({ AGENTSMITH_BACKEND_URL: "server:8081" })).toContain(
      "AGENTSMITH_BACKEND_URL",
    );
  });

  it("Entrypoint_BackendUrlIsAnIpv6Literal_RefusesToStart", () => {
    // Next reads the colons as destination parameters and fails every request.
    expect(runRefused({ AGENTSMITH_BACKEND_URL: "http://[::1]:8081" })).toContain("colon");
  });

  it("Entrypoint_BackendUrlWithSurroundingSpace_WritesTheParsedUrl", () => {
    run({ AGENTSMITH_BACKEND_URL: " http://server:8081 " });

    expect(destinations()["/api/:path*"]).toBe("http://server:8081/api/:path*");
  });

  it("Entrypoint_ManifestMissing_RefusesToStart", () => {
    rmSync(routesManifest);

    expect(runRefused({ AGENTSMITH_BACKEND_URL: "http://server:8081" })).toContain(
      "no routes manifest",
    );
  });

  it("Entrypoint_ProxyRuleMissing_RefusesToStart", () => {
    writeManifest(builtRewrites().filter((rule) => rule.source !== "/hub/:path*"));

    expect(runRefused({ AGENTSMITH_BACKEND_URL: "http://server:8081" })).toContain("/hub/:path*");
  });

  it("NextConfig_ItsRewrites_AreExactlyTheRulesTheEntrypointRewrites", async () => {
    // The entrypoint names the two sources it rewrites and writes backend + source; a
    // rule added to next.config.ts that it does not know would keep its build-time
    // target, and a rule mapping to another path would be overwritten with the source.
    const previous = process.env.AGENTSMITH_BACKEND_URL;
    process.env.AGENTSMITH_BACKEND_URL = "http://backend.test";
    let rewrites: Rewrite[];
    try {
      rewrites = (await nextConfig.rewrites!()) as Rewrite[];
    } finally {
      if (previous === undefined) delete process.env.AGENTSMITH_BACKEND_URL;
      else process.env.AGENTSMITH_BACKEND_URL = previous;
    }

    expect(rewrites.map((rule) => rule.source).sort()).toEqual(["/api/:path*", "/hub/:path*"]);
    for (const rule of rewrites) expect(rule.destination).toBe(`http://backend.test${rule.source}`);
    const entrypoint = readFileSync(ENTRYPOINT, "utf8");
    for (const rule of rewrites) expect(entrypoint).toContain(`"${rule.source}"`);
  });
});
