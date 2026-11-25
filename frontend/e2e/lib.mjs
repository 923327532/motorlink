import puppeteer from 'puppeteer-core';
import fs from 'node:fs';
import path from 'node:path';

export const BASE = process.env.BASE ?? 'http://localhost:5090';
export const SHOTS = path.resolve('shots');
fs.mkdirSync(SHOTS, { recursive: true });

export const VIEWPORTS = {
    desktop: { width: 1440, height: 900, deviceScaleFactor: 1 },
    tablet: { width: 820, height: 1180, deviceScaleFactor: 1 },
    mobile: { width: 390, height: 844, deviceScaleFactor: 2, isMobile: true, hasTouch: true }
};

export async function launch() {
    return puppeteer.launch({
        executablePath: process.env.CHROME_PATH ?? 'C:/Program Files/Google/Chrome/Application/chrome.exe',
        headless: 'new',
        args: ['--no-sandbox', '--disable-gpu', '--lang=es-PE']
    });
}

/** Abre una página registrando errores de consola, excepciones y solicitudes fallidas. */
export async function open(browser, viewport = 'desktop') {
    const ctx = await browser.createBrowserContext();   // cookies aisladas por actor
    const page = await ctx.newPage();
    await page.setViewport(VIEWPORTS[viewport]);
    const problems = [];
    page.on('console', m => { if (['error', 'warning'].includes(m.type())) problems.push(`[console.${m.type()}] ${m.text()}`); });
    page.on('pageerror', e => problems.push(`[pageerror] ${e.message}`));
    page.on('requestfailed', r => problems.push(`[requestfailed] ${r.url()} ${r.failure()?.errorText}`));
    page.on('response', r => { if (r.status() >= 400 && !r.url().includes('favicon')) problems.push(`[http ${r.status()}] ${r.request().method()} ${r.url()}`); });
    page.problems = problems;
    return page;
}

export async function shot(page, name, full = true) {
    const file = path.join(SHOTS, `${name}.png`);
    await page.screenshot({ path: file, fullPage: full });
    return file;
}

export async function login(page, email, password = 'Demo#12345') {
    await page.goto(`${BASE}/ingresar`, { waitUntil: 'networkidle0' });
    await page.type('#Email', email);
    await page.type('#Password', password);
    await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('button[type=submit].btn-lg')]);
}

export const sleep = ms => new Promise(r => setTimeout(r, ms));
