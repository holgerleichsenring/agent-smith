import type { NextConfig } from "next";

// p0169f-followup: browser hits localhost:3000; SignalR hub lives on the
// backend (localhost:8081 in dev, server:8081 in compose). The rewrite
// proxies /hub/* through the Next.js server so the browser stays
// same-origin (no CORS, no separate WS endpoint to remember). Target is
// configurable via AGENTSMITH_BACKEND_URL; defaults match local dev.
// Next.js rewrites forward WebSocket upgrades transparently since v12.
//
// 2026-11-25-7f25a: with output "standalone", rewrites() runs at `next build`,
// not at server boot; the resolved destinations land in
// .next/routes-manifest.json, which is the only place the production server
// reads them from. `next dev` evaluates this function itself, so the variable
// works there as written. The image's docker-entrypoint.sh rewrites the two
// destinations from AGENTSMITH_BACKEND_URL at container start — keep these two
// sources in step with the list it rewrites (dockerEntrypoint.test.ts checks it).

const nextConfig: NextConfig = {
  output: "standalone",
  reactStrictMode: true,
  async rewrites() {
    const backendUrl = process.env.AGENTSMITH_BACKEND_URL ?? "http://localhost:8081";
    return [
      { source: "/hub/:path*", destination: `${backendUrl}/hub/:path*` },
      { source: "/api/:path*", destination: `${backendUrl}/api/:path*` },
    ];
  },
};

export default nextConfig;
