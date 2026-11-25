import { launch, open, login, shot, sleep, BASE } from './lib.mjs';
const b = await launch();

async function check(page, url, name) {
    await page.goto(`${BASE}${url}`, { waitUntil: 'networkidle0' });
    await sleep(800);
    const info = await page.evaluate(() => [...document.querySelectorAll('canvas[data-chart]')].map(c => {
        const ch = Chart.getChart(c);
        const spec = JSON.parse(c.dataset.chart);
        ch.options.animation = false; ch.update('none');     // dibujado final sin animación
        return { type: spec.type, labels: spec.labels.length, series: spec.datasets.map(d => `${d.label ?? ''}:${d.data.join(',')}`) };
    }));
    console.log(name, JSON.stringify(info));
    await sleep(300);
    await shot(page, `final-${name}`);
}

const p = await open(b);
await login(p, 'ventas@autoperu.demo.motorlink.example');
await check(p, '/panel', 'user-dashboard');
const a = await open(b);
await login(a, 'admin@motorlink.local', 'Admin#12345');
await check(a, '/admin', 'admin-dashboard');
await b.close();
