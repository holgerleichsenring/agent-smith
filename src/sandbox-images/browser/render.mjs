// 2026-10-01-283de: render_reference's script, baked into the browser image. node is handed ONE
// path — a JSON request the server wrote — and the URL, the set and the selectors travel inside
// it. It renders the source at a desktop and a mobile viewport, reports the computed styles of
// each selector as the browser computed them, console errors, failed requests and every request
// the egress guard refused, writes base64 screenshots beside its result, and exits.
// 2026-10-01-283di: a request carrying `compare` renders two sources instead (compare.mjs).
import fs from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright-core';
import { compare } from './compare.mjs';
import { EgressGuard } from './egress.mjs';
import { open, styleRows, targetOf } from './pages.mjs';
import { capture, encode, viewports } from './shots.mjs';

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

async function render() {
  const guard = new EgressGuard();
  report.refused = guard.refused;
  const browser = await launch(await guard.listen());
  const encoder = await (await browser.newContext({ offline: true })).newPage();
  // 2026-10-01-283di: a comparison request renders both of its sides instead.
  if (request.compare) {
    await compare(request, browser, guard, encoder, report);
    await browser.close();
    return;
  }
  const url = await targetOf(request, guard);
  for (const viewport of viewports) {
    const page = await open(browser, viewport, report, url);
    if (viewport.name === 'desktop') {
      report.url = page.url();
      report.title = await page.title();
      report.styles = await styleRows(page, request.selectors, request.properties);
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
