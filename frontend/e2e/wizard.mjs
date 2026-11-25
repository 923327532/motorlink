import { launch, open, shot, login, sleep, BASE } from './lib.mjs';
import fs from 'node:fs';
import path from 'node:path';

const results = [];
const ok = (name, cond, extra = '') => { results.push(cond); console.log(`${cond ? '✓' : '✗'} ${name}${extra ? ' — ' + extra : ''}`); };
const browser = await launch();
const tmp = path.resolve('tmp'); fs.mkdirSync(tmp, { recursive: true });

// Imágenes PNG válidas generadas con el propio navegador
async function makePng(file, color) {
    const p = await open(browser);
    await p.setContent(`<body style="margin:0"><div style="width:640px;height:480px;background:${color}"></div></body>`);
    await p.screenshot({ path: file, clip: { x: 0, y: 0, width: 640, height: 480 } });
    await p.close();
}
const png1 = path.join(tmp, 'a.png'), png2 = path.join(tmp, 'b.png'), png3 = path.join(tmp, 'c.png'), fake = path.join(tmp, 'fake.png');
await makePng(png1, '#2563EB'); await makePng(png2, '#16A34A'); await makePng(png3, '#D97706');
fs.writeFileSync(fake, 'esto no es una imagen, es texto renombrado');

const next = page => page.click('[data-next]');
const waitStep = async (page, n) => page.waitForFunction(k => !document.querySelector(`[data-step="${k}"]`)?.hidden, { timeout: 15000 }, n);
const plateOf = () => 'W' + Math.random().toString(36).slice(2, 7).toUpperCase();

async function runWizard(email, label, { expectLimit = false } = {}) {
    const page = await open(browser);
    await login(page, email);
    await page.goto(`${BASE}/panel/publish`, { waitUntil: 'networkidle0' });
    ok(`${label}: el asistente muestra 5 pasos`, (await page.$$('[data-stepper]')).length === 5);

    // Paso 1: validación en cliente y reglas dinámicas
    await next(page); await sleep(300);
    ok(`${label}: paso 1 no avanza vacío y marca errores`, (await page.$$('.field-error')).length > 0 && !(await page.$eval('[data-step="1"]', n => n.hidden)));
    await page.select('#vehicleTypeId', '8');   // mototaxi: sin puertas
    ok(`${label}: mototaxi oculta puertas`, await page.$eval('[data-field-wrap="doors"]', n => n.hidden));
    await page.select('#vehicleTypeId', '1');
    ok(`${label}: auto muestra puertas`, !(await page.$eval('[data-field-wrap="doors"]', n => n.hidden)));
    await page.select('#brandId', '1');
    await page.waitForFunction(() => document.querySelector('#modelId').options.length > 1 && !document.querySelector('#modelId').disabled, { timeout: 10000 });
    const models = await page.$$eval('#modelId option', o => o.map(x => x.textContent));
    ok(`${label}: modelos filtrados por marca y tipo`, models.includes('Corolla') && !models.includes('Hilux'), models.join(','));
    await page.select('#modelId', '2');
    await page.type('#manufactureYear', '2021');
    ok(`${label}: año del modelo se autocompleta`, (await page.$eval('#modelYear', i => i.value)) === '2021');
    await page.select('#condition', 'USED');
    await page.type('#mileage', '25000'); await page.type('#plate', plateOf()); await page.type('#color', 'Gris');
    await next(page); await waitStep(page, 2);

    // Paso 2
    await page.select('#fuelType', 'GASOLINE'); await page.select('#transmission', 'AUTOMATIC');
    await page.type('#engineDisplacementCc', '1800'); await page.type('#doors', '9'); await page.type('#seats', '5');
    await next(page); await sleep(400);
    ok(`${label}: puertas fuera de rango marcadas en el paso 2`, (await page.$eval('#doors', i => i.classList.contains('is-invalid'))));
    await page.$eval('#doors', i => { i.value = '4'; });
    await next(page); await waitStep(page, 3);
    ok(`${label}: el vehículo se guardó (URL de borrador)`, /\/panel\/publish\/[0-9a-f-]{36}/.test(page.url()), page.url());

    // Paso 3
    await next(page); await sleep(300);
    ok(`${label}: exige elegir una modalidad`, !(await page.$eval('[data-wizard-error]', n => n.hidden)));
    ok(`${label}: título sugerido automáticamente`, (await page.$eval('#title', i => i.value)).includes('Corolla'));
    await page.click('[data-op-toggle="SALE"]'); await page.click('[data-op-toggle="WORK_RENT"]');
    await page.type('#sale-price', '55000'); await page.type('#work-daily', '60');
    await page.type('#work-license', 'A-IIb'); await page.type('#work-zone', 'Lima');
    await shot(page, `wizard-step3-${label}`);
    await next(page); await waitStep(page, 4);

    // Paso 4
    await page.type('#phoneNumber', '987000111');
    await page.select('#departmentId', '15');
    await page.waitForFunction(() => document.querySelector('#provinceId').options.length > 1, { timeout: 10000 });
    await page.select('#provinceId', '1501');
    await page.waitForFunction(() => document.querySelector('#districtId').options.length > 1, { timeout: 10000 });
    await page.select('#districtId', '150140');
    await next(page); await waitStep(page, 5);

    // Paso 5: fotos
    const input = await page.$('[data-photo-input]');
    await input.uploadFile(fake); await sleep(1500);
    const toastBad = await page.$eval('#toast-stack', n => n.textContent);
    ok(`${label}: archivo que no es imagen es rechazado por contenido`, /Formato no permitido|no se pudo|contenido/i.test(toastBad) && (await page.$$('.photo-tile')).length === 0, toastBad.trim().slice(0, 90));
    await input.uploadFile(png1, png2, png3); await page.waitForFunction(() => document.querySelectorAll('.photo-tile').length === 3, { timeout: 20000 });
    ok(`${label}: se suben 3 fotografías`, true);
    ok(`${label}: primera es portada`, await page.$eval('.photo-tile:first-child', n => n.classList.contains('is-cover')));
    await page.click('.photo-tile:nth-child(2) button[aria-label="Usar como portada"]'); await sleep(900);
    ok(`${label}: cambiar portada`, await page.$eval('.photo-tile:nth-child(2)', n => n.classList.contains('is-cover')));
    await page.click('.photo-tile:nth-child(1) button[aria-label="Mover a la derecha"]'); await sleep(900);
    ok(`${label}: reordenar`, await page.$eval('.photo-tile:nth-child(2)', n => n.querySelector('img').alt.includes('Fotografía 2')));
    await page.click('.photo-tile:nth-child(3) button[aria-label="Eliminar fotografía"]'); await sleep(500);
    await page.click('#confirm-modal .btn-danger'); await sleep(1200);
    ok(`${label}: eliminar fotografía (con confirmación)`, (await page.$$('.photo-tile')).length === 2);
    await shot(page, `wizard-step5-${label}`);

    await page.click('[data-submit]'); await sleep(1800);
    if (expectLimit) {
        const notice = await page.$eval('[data-limit-notice]', n => ({ hidden: n.hidden, text: n.textContent }));
        ok(`${label}: plan gratuito bloquea el envío (límite de publicaciones)`, !notice.hidden && /publicaciones activas/.test(notice.text), notice.text.trim().slice(0, 100));
    } else {
        ok(`${label}: envío a revisión redirige a Mis publicaciones`, page.url().includes('/panel/listings'), page.url());
    }
    ok(`${label}: sin errores JS`, page.problems.filter(p => !/http 40d|Failed to load resource: the server responded with a status of 40/.test(p)).length === 0, page.problems.join(' | '));
    return page;
}

try {
    await runWizard('lucia.paredes@demo.motorlink.example', 'lucia');   // plan BÁSICO: puede enviar
    // El moderador aprueba desde la UI de administración
    const admin = await open(browser);
    await login(admin, 'admin@motorlink.local', 'Admin#12345');
    await admin.goto(`${BASE}/admin/listings`, { waitUntil: 'networkidle0' });
    const pending = (await admin.$$('form[action*="approve" i] button')).length;
    ok('Admin: cola de moderación lista la publicación', pending >= 1, `${pending} por revisar`);
    await shot(admin, 'admin-moderation');
    await admin.evaluate(() => { const row = [...document.querySelectorAll('tbody tr')].find(r => r.textContent.includes('Corolla')); row.querySelector('form[action*="approve" i] button').click(); }); await sleep(600);
    await Promise.all([admin.waitForNavigation({ waitUntil: 'networkidle0' }), admin.click('#confirm-modal .btn-primary')]);
    ok('Admin: aprobar muestra confirmación', (await admin.$eval('#toast-stack', n => n.textContent).catch(() => '')).includes('aprobada'));
    const pub = await open(browser);
    await pub.goto(`${BASE}/vehiculos?q=Corolla`, { waitUntil: 'networkidle0' });
    ok('La publicación aprobada aparece en el buscador público', (await pub.$$('.listing-card')).length >= 1);

    await runWizard('rosa.quispe@demo.motorlink.example', 'rosa-gratis', { expectLimit: true });
} finally { await browser.close(); }
console.log(results.every(Boolean) ? '\nTodas las verificaciones pasaron' : `\n${results.filter(r => !r).length} verificaciones fallidas`);
