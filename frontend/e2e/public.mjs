import { launch, open, shot, BASE } from './lib.mjs';

const browser = await launch();
const report = {};
try {
    // Obtiene un id real de publicación desde el propio sitio
    const probe = await open(browser);
    await probe.goto(`${BASE}/vehiculos`, { waitUntil: 'networkidle0' });
    const detailHref = await probe.$eval('.listing-card__title a', a => a.getAttribute('href'));
    const workHref = await (async () => { await probe.goto(`${BASE}/vehiculos?operation=WORK_RENT`, { waitUntil: 'networkidle0' }); return probe.$eval('.listing-card__title a', a => a.getAttribute('href')); })();
    await probe.close();

    for (const vp of (process.env.VPS ?? 'desktop,tablet,mobile').split(',')) {
        const pages = [['home', '/'], ['search', '/vehiculos'], ['search-work', '/vehiculos?operation=WORK_RENT&currency=PEN&priceMax=80'], ['detail', detailHref], ['detail-work', workHref], ['plans', '/planes'], ['login', '/ingresar'], ['register', '/registro'], ['terms', '/terminos']];
        for (const [name, url] of pages) {
            const page = await open(browser, vp);
            await page.goto(`${BASE}${url}`, { waitUntil: 'networkidle0' });
            await page.evaluate(async () => { for (let y = 0; y < document.body.scrollHeight; y += 500) { window.scrollTo(0, y); await new Promise(r => setTimeout(r, 60)); } window.scrollTo(0, 0); });
            await new Promise(r => setTimeout(r, 400));
            // sin desbordamiento horizontal
            const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
            // imágenes rotas
            const broken = await page.evaluate(() => [...document.images].filter(i => i.complete && i.naturalWidth === 0).map(i => i.src));
            await shot(page, `${vp}-${name}`);
            report[`${vp}/${name}`] = { problems: page.problems, overflow, broken };
            await page.close();
        }
    }
} finally { await browser.close(); }

let bad = 0;
for (const [k, v] of Object.entries(report)) {
    const issues = [...v.problems, ...v.broken.map(b => `[img rota] ${b}`), ...(v.overflow > 1 ? [`[desborde horizontal ${v.overflow}px]`] : [])];
    if (issues.length) { bad++; console.log(`✗ ${k}\n   ${issues.join('\n   ')}`); } else console.log(`✓ ${k}`);
}
console.log(bad ? `\n${bad} páginas con problemas` : '\nSin problemas');
