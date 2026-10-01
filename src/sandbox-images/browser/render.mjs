// 2026-10-01-283de: render_reference's script, baked into the browser image. node is handed ONE
// path — a JSON request the server wrote — and the URL, the set and the selectors travel inside
// it. It renders the source at a desktop and a mobile viewport, reports the computed styles of
// each selector as the browser computed them, console errors, failed requests and every request
// the egress guard refused, writes base64 screenshots beside its result, and exits.
import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright-core';
import { EgressGuard } from './egress.mjs';
import { capture, encode, viewports } from './shots.mjs';
import { locate, serve } from './site.mjs';

const watchdogMs = 200_000;
const request = JSON.parse(await fs.readFile(process.argv[2], 'utf8'));
const out = request.outDir;
await fs.mkdir(out, { recursive: true });
const report = { url: null, title: null, styles: [], consoleErrors: [], failedRequests: [], refused: [], shots: [] };
// Both viewports load the page, so each failure is seen twice; it is reported once.
const once = (items, key) => [...new Map(items.map(item => [key(item), item])).values()];
const finish = async code => {
  report.consoleErrors = once(report.consoleErrors, e => e);
  report.failedRequests = once(report.failedRequests, r => `${r.url}\n${r.reason}`);
  report.refused = once(report.refused, r => `${r.url}\n${r.reason}`);
  await fs.writeFile(path.join(out, 'result.json'), JSON.stringify(report));
  process.exit(code);
};
setTimeout(() => { report.error = `the render did not finish within ${watchdogMs / 1000}s`; finish(2); }, watchdogMs).unref();

async function target(guard) {
  if (request.url) return request.url;
  const site = await locate(path.resolve(request.siteDir), request.page ?? '');
  const port = await serve(site.root);
  guard.allowLocal(port);
  return `http://127.0.0.1:${port}${site.path}`;
}

async function launch(proxyPort) {
  const args = [`--proxy-server=http://127.0.0.1:${proxyPort}`, '--proxy-bypass-list=<-loopback>',
    '--disable-dev-shm-usage', '--disable-quic', '--force-webrtc-ip-handling-policy=disable_non_proxied_udp',
    '--webrtc-ip-handling-policy=disable_non_proxied_udp'];
  const asRoot = process.getuid?.() === 0;
  try {
    return await chromium.launch({ args, chromiumSandbox: !asRoot, env: { ...process.env, HOME: process.env.HOME || '/tmp' } });
  } catch (error) {
    if (asRoot || !/sandbox/i.test(String(error))) throw error;
    report.consoleErrors.push('note: no usable Chromium sandbox for this user; rendered with --no-sandbox');
    return chromium.launch({ args, chromiumSandbox: false, env: { ...process.env, HOME: process.env.HOME || '/tmp' } });
  }
}

async function newPage(browser, viewport) {
  const context = await browser.newContext({ viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: 1, isMobile: viewport.isMobile, hasTouch: viewport.isMobile,
    serviceWorkers: 'block', acceptDownloads: false });
  await context.route('**/*', route => /^https?:$/.test(new URL(route.request().url()).protocol)
    ? route.continue() : route.abort('blockedbyclient'));
  const page = await context.newPage();
  page.on('console', message => { if (message.type() === 'error') report.consoleErrors.push(message.text()); });
  page.on('pageerror', error => report.consoleErrors.push(String(error.message ?? error)));
  page.on('requestfailed', r => report.failedRequests.push({ url: r.url(), reason: r.failure()?.errorText ?? 'failed' }));
  page.on('response', r => { if (r.status() >= 400) report.failedRequests.push({ url: r.url(), reason: `HTTP ${r.status()}` }); });
  return page;
}

const styles = page => page.evaluate(({ selectors, properties }) => selectors.map(selector => {
  let nodes;
  try { nodes = document.querySelectorAll(selector); } catch { return { selector, count: 0, values: null }; }
  if (nodes.length === 0) return { selector, count: 0, values: null };
  const computed = getComputedStyle(nodes[0]);
  return { selector, count: nodes.length, values: Object.fromEntries(properties.map(p => [p, computed.getPropertyValue(p)])) };
}), { selectors: request.selectors, properties: request.properties });

async function render() {
  const guard = new EgressGuard();
  report.refused = guard.refused;
  const browser = await launch(await guard.listen());
  const url = await target(guard);
  const encoder = await (await browser.newContext({ offline: true })).newPage();
  for (const viewport of viewports) {
    const page = await newPage(browser, viewport);
    await page.goto(url, { waitUntil: 'load', timeout: 45_000 });
    await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => {});
    if (viewport.name === 'desktop') {
      report.url = page.url();
      report.title = await page.title();
      report.styles = await styles(page);
    }
    const shot = await capture(page, viewport);
    const jpeg = await encode(encoder, shot.png);
    const file = `${viewport.name}.jpg.b64`;
    await fs.writeFile(path.join(out, file), jpeg.base64);
    report.shots.push({ viewport: viewport.name, file, width: jpeg.width, height: jpeg.height,
      bytes: jpeg.bytes, pageHeight: shot.pageHeight, capturedHeight: shot.capturedHeight });
    await page.context().close();
  }
  await browser.close();
}

try {
  await render();
  await finish(0);
} catch (error) {
  report.error = String(error?.message ?? error);
  await finish(1);
}
