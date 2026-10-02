// 2026-10-01-283de: one screenshot per viewport — at most three viewport heights of the page,
// downscaled so the long edge is at most 1568 px, JPEG through a quality ladder until it is under
// 700 KB, so its base64 fits one sandbox read. The browser encodes it: the capture is drawn onto
// a canvas in a page of its own, so the image needs no library beside Chromium.
export const viewports = [
  { name: 'desktop', width: 1440, height: 900, maxCapture: 2700, isMobile: false },
  { name: 'mobile', width: 390, height: 844, maxCapture: 2532, isMobile: true },
];
const maxEdge = 1568;
const maxBytes = 700 * 1024;

export async function capture(page, viewport) {
  const pageHeight = await page.evaluate(() =>
    Math.max(document.documentElement?.scrollHeight ?? 0, document.body?.scrollHeight ?? 0, innerHeight));
  const capturedHeight = Math.min(pageHeight, viewport.maxCapture);
  const png = await page.screenshot({ type: 'png', fullPage: true, animations: 'disabled', timeout: 30000,
    clip: { x: 0, y: 0, width: viewport.width, height: capturedHeight } });
  return { png, pageHeight, capturedHeight };
}

export async function encode(encoder, png) {
  return encoder.evaluate(async ({ source, maxEdge, maxBytes }) => {
    const image = new Image();
    image.src = 'data:image/png;base64,' + source;
    await image.decode();
    let scale = Math.min(1, maxEdge / Math.max(image.width, image.height));
    for (;;) {
      const canvas = document.createElement('canvas');
      canvas.width = Math.max(1, Math.round(image.width * scale));
      canvas.height = Math.max(1, Math.round(image.height * scale));
      const context = canvas.getContext('2d');
      context.imageSmoothingQuality = 'high';
      context.drawImage(image, 0, 0, canvas.width, canvas.height);
      for (const quality of [0.85, 0.75, 0.65, 0.55, 0.45, 0.35]) {
        const url = canvas.toDataURL('image/jpeg', quality);
        const base64 = url.slice(url.indexOf(',') + 1);
        const bytes = Math.floor(base64.length * 3 / 4) - (base64.endsWith('==') ? 2 : base64.endsWith('=') ? 1 : 0);
        if (bytes < maxBytes) return { base64, width: canvas.width, height: canvas.height, bytes };
      }
      scale *= 0.8;
    }
  }, { source: png.toString('base64'), maxEdge, maxBytes });
}
