import { launch, open, shot, login, sleep, BASE } from './lib.mjs';

const browser = await launch();
let bad = 0;

async function sweep(vp, email, password, pages, prefix) {
    const page = await open(browser, vp);
    await login(page, email, password);
    for (const [name, url] of pages) {
        page.problems.length = 0;
        const res = await page.goto(`${BASE}${url}`, { waitUntil: 'networkidle0' });
        await page.evaluate(async () => { for (let y = 0; y < document.body.scrollHeight; y += 600) { window.scrollTo(0, y); await new Promise(r => setTimeout(r, 40)); } window.scrollTo(0, 0); });
        await sleep(500); // deja que Chart.js termine de animar
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
        const broken = await page.evaluate(() => [...document.images].filter(i => i.complete && i.naturalWidth === 0).map(i => i.src));
        const canvases = await page.$$eval('canvas[data-chart]', cs => cs.map(c => c.width > 0 && c.height > 0));
        const title = await page.title();
        await shot(page, `${prefix}-${vp}-${name}`);
        const issues = [...page.problems, ...broken.map(b => `[img rota] ${b}`), ...(overflow > 1 ? [`[desborde ${overflow}px]`] : []), ...(canvases.includes(false) ? ['[gráfico sin dibujar]'] : []),
            ...(res.status() >= 400 ? [`[status ${res.status()}]`] : []), ...(page.url().includes('/ingresar') ? ['[redirigió a login]'] : [])];
        if (issues.length) { bad++; console.log(`✗ ${vp} ${name} (${title})\n   ${issues.join('\n   ')}`); } else console.log(`✓ ${vp} ${name}${canvases.length ? ` (${canvases.length} gráficos)` : ''}`);
    }
    await page.close();
}

const userPages = [['home', '/panel'], ['vehicles', '/panel/vehicles'], ['listings', '/panel/listings'], ['received', '/panel/negotiations/received'], ['sent', '/panel/negotiations/sent'],
    ['favorites', '/panel/favorites'], ['subscription', '/panel/subscription'], ['payments', '/panel/subscription/payments'], ['profile', '/panel/profile'], ['security', '/panel/security'],
    ['notifications', '/panel/notifications'], ['publish', '/panel/publish']];
const adminPages = [['home', '/admin'], ['users', '/admin/users'], ['businesses', '/admin/businesses'], ['vehicles', '/admin/vehicles'], ['listings', '/admin/listings'], ['negotiations', '/admin/negotiations'],
    ['plans', '/admin/plans'], ['subscriptions', '/admin/subscriptions'], ['payments', '/admin/payments'], ['catalog', '/admin/catalog'], ['catalog-models', '/admin/catalog?tab=models'], ['locations', '/admin/locations'],
    ['reports', '/admin/reports'], ['audit', '/admin/audit']];

try {
    for (const vp of ['desktop', 'mobile']) {
        await sweep(vp, 'ventas@autoperu.demo.motorlink.example', 'Demo#12345', userPages, 'u');       // plan Profesional (estadísticas avanzadas)
        await sweep(vp, 'admin@motorlink.local', 'Admin#12345', adminPages, 'a');
    }
    // usuario Gratis: panel con estados de plan limitado
    await sweep('desktop', 'rosa.quispe@demo.motorlink.example', 'Demo#12345', [['home', '/panel'], ['subscription', '/panel/subscription'], ['listings', '/panel/listings']], 'free');

    // Protección de rutas
    const anon = await open(browser);
    for (const url of ['/panel', '/admin', '/admin/plans', '/panel/publish']) {
        await anon.goto(`${BASE}${url}`, { waitUntil: 'networkidle0' });
        const okRedirect = anon.url().includes('/ingresar');
        console.log(`${okRedirect ? '✓' : '✗'} anónimo ${url} → ${anon.url().replace(BASE, '')}`); if (!okRedirect) bad++;
    }
    const user = await open(browser);
    await login(user, 'carlos.mendoza@demo.motorlink.example');
    for (const url of ['/admin', '/admin/users', '/admin/plans']) {
        await user.goto(`${BASE}${url}`, { waitUntil: 'networkidle0' });
        const denied = user.url().includes('acceso-denegado');
        console.log(`${denied ? '✓' : '✗'} usuario normal ${url} → ${user.url().replace(BASE, '')}`); if (!denied) bad++;
    }
    await shot(user, 'access-denied');
} finally { await browser.close(); }
console.log(bad ? `\n${bad} problemas` : '\nSin problemas');
