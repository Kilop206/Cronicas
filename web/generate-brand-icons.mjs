import { readFile, writeFile } from 'node:fs/promises';
import { chromium } from 'playwright';

// Rasterize the existing vector mark; no new logo or image dependency.
const root = new URL('./public/', import.meta.url);
const svg = await readFile(new URL('favicon.svg', root), 'utf8');
const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const page = await browser.newPage({ deviceScaleFactor: 1 });
  for (const [size, file] of [[16, 'favicon-16.png'], [32, 'favicon-32.png'], [48, 'favicon-48.png'], [180, 'apple-touch-icon.png']]) {
    await page.setViewportSize({ width: size, height: size });
    await page.setContent(`<style>body{margin:0;background:#101419}svg{width:100vw;height:100vh;display:block}</style>${svg}`);
    await page.screenshot({ path: new URL(file, root).pathname.replace(/^\/([A-Za-z]:)/, '$1'), type: 'png' });
  }
  const entries = await Promise.all([16, 32, 48].map(size => readFile(new URL(`favicon-${size}.png`, root))));
  const header = Buffer.alloc(6 + 16 * entries.length); header.writeUInt16LE(1, 2); header.writeUInt16LE(entries.length, 4);
  let offset = header.length;
  entries.forEach((data, index) => {
    const start = 6 + index * 16; header[start] = [16, 32, 48][index]; header[start + 1] = header[start];
    header.writeUInt16LE(1, start + 4); header.writeUInt16LE(32, start + 6);
    header.writeUInt32LE(data.length, start + 8); header.writeUInt32LE(offset, start + 12); offset += data.length;
  });
  await writeFile(new URL('favicon.ico', root), Buffer.concat([header, ...entries]));
} finally { await browser.close(); }
