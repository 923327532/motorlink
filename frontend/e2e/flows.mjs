// Flujos autenticados de punta a punta con un navegador real.
import { launch, open, shot, login, sleep, BASE } from './lib.mjs';
import fs from 'node:fs';
import path from 'node:path';

const results = [];
const ok = (name, cond, extra = '') => { results.push([cond, name, extra]); console.log(`${cond ? '✓' : '✗'} ${name}${extra ? ' — ' + extra : ''}`); };

const browser = await launch();
try {
    // ---------- 1. Registro nuevo + login + logout ----------
    {
        const page = await open(browser);
        const email = `e2e${Date.now()}@demo.motorlink.example`;
        await page.goto(`${BASE}/registro`, { waitUntil: 'networkidle0' });
        // validación en cliente (envío vacío)
        await page.click('button[type=submit].btn-lg'); await sleep(400);
        ok('Registro: muestra validaciones en cliente', (await page.$$('.field-validation-error')).some(Boolean) && (await page.$$eval('.field-validation-error', n => n.map(x => x.textContent.trim()).filter(Boolean))).length > 0);
        await page.click('input[value=BUSINESS][data-account-choice]');
        await page.type('#BusinessName', 'E2E Motors');
        await page.type('#FirstName', 'Eva'); await page.type('#LastName', 'Prueba'); await page.type('#Email', email);
        await page.type('#Password', 'Demo#12345'); await page.type('#ConfirmPassword', 'Demo#12345'); await page.click('#AcceptTerms');
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('button[type=submit].btn-lg')]);
        ok('Registro empresarial redirige al panel', page.url().endsWith('/panel'), page.url());
        const cookies = await page.cookies();
        const session = cookies.find(c => c.name === 'MotorLink.Session');
        ok('Cookie de sesión HttpOnly', session?.httpOnly === true);
        ok('Sin tokens en localStorage/sessionStorage', await page.evaluate(() => localStorage.length + sessionStorage.length === 0));
        ok('Sin JWT en el HTML', !(await page.content()).includes('eyJ'));
        await shot(page, 'e2e-dashboard-new-user');
        await page.click('.app-topbar button[aria-label="Menú de tu cuenta"]'); await sleep(400);
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('.app-topbar button.dropdown-item[type=submit]')]);
        await page.goto(`${BASE}/panel`, { waitUntil: 'networkidle0' });
        ok('Tras cerrar sesión /panel exige login', page.url().includes('/ingresar'), page.url());
        ok('Registro/login sin errores JS', page.problems.filter(p => !p.includes('http 401') && !p.includes('http 302')).length === 0, page.problems.join(' | '));
        await page.close();
    }

    // ---------- 2. Login inválido, antiforgery ----------
    {
        const page = await open(browser);
        await page.goto(`${BASE}/ingresar`, { waitUntil: 'networkidle0' });
        await page.type('#Email', 'comprador@demo.motorlink.example'); await page.type('#Password', 'incorrecta');
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('button[type=submit].btn-lg')]);
        const msg = await page.$eval('.validation-summary-errors', n => n.textContent).catch(() => '');
        ok('Login inválido muestra mensaje seguro', msg.includes('Correo o contraseña incorrectos'), msg.trim());
        const status = await page.evaluate(async () => (await fetch('/ingresar', { method: 'POST', body: new URLSearchParams({ Email: 'a@b.com', Password: 'x' }) })).status);
        ok('POST sin token antiforgery rechazado (400)', status === 400, String(status));
        await page.close();
    }

    // ---------- 3. Comprador: favoritos, propuesta, negociación ----------
    {
        const page = await open(browser);
        await login(page, 'comprador@demo.motorlink.example');
        ok('Login comprador', page.url().endsWith('/panel'), page.url());

        await page.goto(`${BASE}/vehiculos?q=Kia`, { waitUntil: 'networkidle0' });
        const fav = await page.$('[data-favorite]');
        const before = await fav.evaluate(b => b.getAttribute('aria-pressed'));
        await fav.click(); await sleep(900);
        const after = await fav.evaluate(b => b.getAttribute('aria-pressed'));
        ok('Favorito alterna estado', before !== after, `${before} → ${after}`);
        await page.reload({ waitUntil: 'networkidle0' });
        ok('Favorito persiste tras recargar', (await page.$eval('[data-favorite]', b => b.getAttribute('aria-pressed'))) === after);
        await page.$eval('[data-favorite]', b => b.click()); await sleep(700);   // restablece

        // detalle de una venta no negociada aún por este usuario (Mazda 3)
        await page.goto(`${BASE}/vehiculos?q=${process.env.TARGET ?? 'CR-V'}`, { waitUntil: 'networkidle0' });
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('.listing-card__title a')]);
        await page.click('[data-bs-target="#offer-modal"]'); await sleep(500);
        await page.type('#offer-amount', '78000'); await page.type('#offer-msg', 'Oferta de prueba E2E');
        await shot(page, 'e2e-offer-modal', false);
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('[data-offer-form] button[type=submit]')]);
        ok('Enviar propuesta crea la negociación', page.url().toLowerCase().includes('/panel/negotiations/details/'), page.url());
        ok('Propuesta aparece en el historial', (await page.content()).includes('78,000') || (await page.content()).includes('78 000'));
        await shot(page, 'e2e-negotiation-pending');
        // una segunda propuesta igual debe rechazarse con mensaje
        await page.goBack({ waitUntil: 'networkidle0' });
        await page.click('[data-bs-target="#offer-modal"]'); await sleep(400);
        await page.$eval('#offer-amount', i => { i.value = '70000'; });
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('[data-offer-form] button[type=submit]')]);
        await sleep(600);
        const toast = await page.$eval('#toast-stack', n => n.textContent).catch(() => '');
        ok('Segunda propuesta abierta rechazada con aviso', toast.includes('negociación abierta'), toast.trim().slice(0, 80));

        await page.goto(`${BASE}/panel/negotiations/sent`, { waitUntil: 'networkidle0' });
        await shot(page, 'e2e-negotiations-sent');
        await page.goto(`${BASE}/panel/favorites`, { waitUntil: 'networkidle0' });
        ok('Favoritos del panel listan publicaciones', (await page.$$('.listing-card')).length >= 5);
        ok('Buyer: sin errores JS', page.problems.filter(p => !p.includes('http 409')).length === 0, page.problems.join(' | '));
        await page.close();
    }

    // ---------- 4. Propietario: responde la propuesta (contrapropuesta y acuerdo) ----------
    {
        const page = await open(browser);
        await login(page, 'ventas@autoperu.demo.motorlink.example');
        await page.goto(`${BASE}/panel/negotiations/received`, { waitUntil: 'networkidle0' });
        const links = await page.$$eval('a[href*="/negotiations/details/"]', a => a.map(x => x.getAttribute('href')));
        ok('Propietario ve propuestas recibidas', links.length >= 2, `${links.length} filas`);
        // la propuesta "Tu turno" (Mazda 3)
        await page.goto(`${BASE}/panel/negotiations/received?status=PENDING`, { waitUntil: 'networkidle0' });
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('a.btn-primary[href*="/negotiations/details/"]')]);
        await shot(page, 'e2e-negotiation-owner');
        const canCounter = await page.$('#counter-amount');
        ok('Propietario ve acciones (aceptar/contraofertar/rechazar)', !!canCounter && !!(await page.$('button.btn-success')));
        await page.type('#counter-amount', '80000');
        await Promise.all([page.waitForNavigation({ waitUntil: 'networkidle0' }), page.click('form[action*="Counter"] button[type=submit], form[action*="counter" i] button[type=submit]')]);
        const html = await page.content();
        ok('Contrapropuesta registrada (historial de 2 ofertas)', (html.match(/class="offer /g) ?? []).length === 2);
        ok('Tras contraofertar, el propietario ya no puede aceptar su propia oferta', !(await page.$('button.btn-success')));
        await page.close();

        // el comprador acepta
        const buyer = await open(browser);
        await login(buyer, 'comprador@demo.motorlink.example');
        await buyer.goto(`${BASE}/panel/negotiations/sent?status=COUNTER_OFFER`, { waitUntil: 'networkidle0' });
        const hrefs = await buyer.$$eval('a[href*="/negotiations/details/"]', a => a.map(x => x.getAttribute('href')));
        let accepted = false;
        for (const h of hrefs) {
            await buyer.goto(`${BASE}${h}`, { waitUntil: 'networkidle0' });
            if ((await buyer.content()).includes(process.env.TARGET ?? 'CR-V')) {
                await buyer.click('button.btn-success'); await sleep(500);
                await Promise.all([buyer.waitForNavigation({ waitUntil: 'networkidle0' }), buyer.click('#confirm-modal .btn-primary')]);
                accepted = (await buyer.content()).includes('Acuerdo de precio');
                break;
            }
        }
        ok('Comprador acepta y se muestra el precio acordado', accepted);
        const text = await buyer.$eval('.agreement', n => n.textContent).catch(() => '');
        ok('Acuerdo no afirma pago/entrega', /No es una confirmación de pago ni de entrega/.test(text));
        await shot(buyer, 'e2e-negotiation-agreement');
        await buyer.close();
    }
} finally { await browser.close(); }
const failed = results.filter(r => !r[0]).length;
console.log(failed ? `\n${failed} verificaciones fallidas` : '\nTodas las verificaciones pasaron');
