#!/usr/bin/env node
// 截取桌機與窄螢幕完整畫面，並回報可機械判斷的問題。它不評美感。
// 用法：node render-check.mjs <url|html 檔> [--out 目錄] [--playwright 模組路徑] [--chrome 瀏覽器路徑]
//       [--viewports 1440x1000,390x844] [--settle 毫秒]
import { createRequire } from 'node:module';
import { mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';

const args = process.argv.slice(2);
const opt = (name, fallback) => { const i = args.indexOf(`--${name}`); return i >= 0 ? args[i + 1] : fallback; };
const target = args.find((a, i) => !a.startsWith('--') && !args[i - 1]?.startsWith('--'));
if (!target) {
  console.error('需要 URL 或 HTML 檔路徑。例：node render-check.mjs http://127.0.0.1:4321/ --out .render-check');
  process.exit(2);
}
const url = /^[a-z]+:\/\//i.test(target) ? target : pathToFileURL(resolve(target)).href;
const out = resolve(opt('out', '.render-check'));
const settle = Number(opt('settle', '1500'));
const viewports = opt('viewports', '1440x1000,390x844').split(',').map(v => v.split('x').map(Number));

const require = createRequire(import.meta.url);
let playwright;
for (const name of [opt('playwright'), process.env.PLAYWRIGHT_MODULE, 'playwright', 'playwright-core'].filter(Boolean)) {
  try { playwright = require(name); break; } catch {}
}
if (!playwright) {
  console.error('找不到 Playwright。用專案已有的安裝，或以 --playwright <node_modules/playwright 路徑> 指定。');
  process.exit(2);
}

async function launch() {
  const executablePath = opt('chrome', process.env.CHROME_PATH);
  if (executablePath) return playwright.chromium.launch({ executablePath });
  try { return await playwright.chromium.launch(); } catch (error) {
    try { return await playwright.chromium.launch({ channel: 'chrome' }); } catch { throw error; }
  }
}

// 在頁面內執行；只回報可重現的量測值，交由人或模型判斷。
function inspect() {
  const describe = el => {
    const id = el.id ? `#${el.id}` : '';
    const cls = typeof el.className === 'string' && el.className.trim() ? `.${el.className.trim().split(/\s+/).slice(0, 2).join('.')}` : '';
    const text = (el.innerText || el.getAttribute('aria-label') || '').trim().replace(/\s+/g, ' ').slice(0, 40);
    return `${el.tagName.toLowerCase()}${id}${cls}${text ? ` “${text}”` : ''}`;
  };
  const visible = el => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; };
  const rgba = c => (c.match(/[\d.]+/g) || []).map(Number);
  const lum = ([r, g, b]) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
  const background = el => {
    for (let node = el; node; node = node.parentElement) {
      const s = getComputedStyle(node);
      if (s.backgroundImage !== 'none') return null; // 圖片或漸層上的文字無法可靠計算
      const c = rgba(s.backgroundColor);
      if (c.length >= 3 && (c[3] ?? 1) >= 0.99) return c;
      if (c.length >= 4 && c[3] > 0) return null; // 半透明底疊在其他內容上，無法可靠計算
    }
    return [255, 255, 255];
  };
  const unique = list => [...new Set(list)];
  const doc = document.documentElement;
  const all = [...document.body.querySelectorAll('*')].filter(visible);
  const textEls = all.filter(el => [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()));
  const lowContrast = [];
  let unknownContrast = 0;
  for (const el of textEls) {
    if (el.closest(':disabled, [aria-disabled="true"]')) continue; // WCAG 不要求停用控制項的對比
    const s = getComputedStyle(el);
    const fg = rgba(s.color);
    const bg = background(el);
    if (!bg || (fg[3] ?? 1) < 0.99) { unknownContrast++; continue; }
    const [a, b] = [lum(fg), lum(bg)].sort((x, y) => y - x);
    const ratio = (a + 0.05) / (b + 0.05);
    const size = parseFloat(s.fontSize);
    const large = size >= 24 || (size >= 18.66 && Number(s.fontWeight) >= 700);
    if (ratio < (large ? 3 : 4.5)) lowContrast.push({ element: describe(el), ratio: Math.round(ratio * 100) / 100, fontSize: size });
  }
  const clipped = all.filter(el => {
    const s = getComputedStyle(el);
    if (el.clientWidth <= 1 || el.clientHeight <= 1) return false; // 僅供螢幕閱讀器的隱藏文字
    const clips = /hidden|clip/.test(s.overflowX + s.overflowY);
    return clips && el.innerText?.trim() && (el.scrollWidth > el.clientWidth + 1 || el.scrollHeight > el.clientHeight + 1);
  });
  const named = el => (el.textContent || '').trim() || el.getAttribute('aria-label') || el.getAttribute('aria-labelledby') || el.getAttribute('title') || el.querySelector('img[alt]:not([alt=""])') || (el.id && document.querySelector(`label[for="${CSS.escape(el.id)}"]`)) || el.closest('label');
  const controls = [...document.querySelectorAll('button, a[href], input:not([type=hidden]), select, textarea, [role=button]')].filter(visible);
  return {
    viewport: { width: innerWidth, height: innerHeight, documentHeight: doc.scrollHeight },
    horizontalOverflow: doc.scrollWidth > doc.clientWidth
      ? { scrollWidth: doc.scrollWidth, clientWidth: doc.clientWidth, widest: all.filter(el => el.getBoundingClientRect().right > doc.clientWidth + 1).slice(0, 5).map(describe) }
      : null,
    brokenImages: [...document.images].filter(img => img.getAttribute('src') && img.complete && img.naturalWidth === 0).map(img => img.currentSrc || img.src),
    imagesWithoutAlt: [...document.images].filter(img => !img.hasAttribute('alt')).map(img => img.currentSrc || img.src).slice(0, 10),
    unnamedControls: controls.filter(el => !named(el)).slice(0, 10).map(describe),
    clippedText: unique(clipped.map(describe)).slice(0, 10),
    smallText: unique(textEls.filter(el => parseFloat(getComputedStyle(el).fontSize) < 12).map(describe)).slice(0, 10),
    lowContrast: lowContrast.slice(0, 15),
    contrastNotComputed: unknownContrast,
    fontsUsed: [...new Set(textEls.slice(0, 400).map(el => getComputedStyle(el).fontFamily.split(',')[0].trim()))].slice(0, 8),
    fontsLoaded: [...document.fonts].filter(f => f.status === 'loaded').map(f => `${f.family} ${f.weight}`).slice(0, 12),
  };
}

mkdirSync(out, { recursive: true });
const browser = await launch();
const report = { url, capturedAt: new Date().toISOString(), note: '機械檢查不評美感；對比只計算純色背景上的文字。', results: [] };
try {
  for (const [width, height] of viewports) {
    const page = await browser.newPage({ viewport: { width, height } });
    const errors = [];
    const failed = [];
    page.on('pageerror', e => errors.push(e.message));
    page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
    page.on('requestfailed', r => failed.push(`${r.url()} ${r.failure()?.errorText || ''}`.trim()));
    await page.goto(url, { waitUntil: 'load' });
    await page.evaluate(async () => {
      await document.fonts.ready;
      // 滾動一次觸發 lazy 圖片與進場動畫，再回到頂部等動畫結束，避免把動畫途中的隱藏當成缺失。
      for (let y = 0; y < document.documentElement.scrollHeight; y += innerHeight) { scrollTo(0, y); await new Promise(r => setTimeout(r, 60)); }
      scrollTo(0, 0);
      await Promise.race([Promise.allSettled(document.getAnimations().filter(a => a.effect?.getComputedTiming().iterations !== Infinity).map(a => a.finished)), new Promise(r => setTimeout(r, 4000))]);
    });
    await page.waitForTimeout(settle);
    const file = join(out, `${width}x${height}.png`);
    await page.screenshot({ path: file, fullPage: true });
    report.results.push({ screenshot: file, errors, failedRequests: failed, ...(await page.evaluate(inspect)) });
    await page.close();
  }
} finally {
  await browser.close();
}
writeFileSync(join(out, 'report.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
