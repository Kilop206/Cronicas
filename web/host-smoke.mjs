import { chromium } from 'playwright';

const url = process.argv[2];
if (!url || !url.startsWith('https://')) throw new Error('Informe a URL HTTPS do site.');

const browser = await chromium.launch({ channel: 'chrome', headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  const response = await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 });
  const visibleText = (await page.locator('body').innerText()).trim();
  const result = { status: response?.status(), title: await page.title(), visibleCharacters: visibleText.length, errors };
  console.log(JSON.stringify(result));
  if (result.status !== 200 || visibleText.length < 100 || errors.length) process.exitCode = 1;
} finally {
  await browser.close();
}
