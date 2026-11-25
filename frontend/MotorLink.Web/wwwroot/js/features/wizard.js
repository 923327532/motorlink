// Asistente de publicación de 5 pasos. Cada paso se valida en el navegador y se guarda en la API;
// los errores definitivos (por campo) los devuelve el backend y se muestran junto a cada control.
import { el } from '../core/dom.js';
import { sendJson, sendForm } from '../core/http.js';
import { toast } from '../ui/toast.js';
import { confirmAction } from '../ui/confirm.js';
import { initIcons } from '../ui/icons.js';

// Reglas técnicas por tipo (reflejo de las del backend; el backend es quien decide).
const TYPE_RULES = {
    CAR:        { doors: [2, 5], seats: [2, 9], load: false, traction: true, disp: true },
    PICKUP:     { doors: [2, 5], seats: [2, 9], load: 'optional', traction: true, disp: true },
    MINIVAN:    { doors: [3, 5], seats: [5, 9], load: false, traction: true, disp: true },
    VAN:        { doors: [2, 5], seats: [2, 15], load: true, traction: true, disp: true },
    TRUCK:      { doors: [2, 4], seats: [1, 3], load: true, traction: true, disp: true },
    BUS:        { doors: [1, 4], seats: [10, 80], load: false, traction: true, disp: true },
    MOTORCYCLE: { doors: null, seats: [1, 2], load: false, traction: false, disp: true },
    MOTOTAXI:   { doors: null, seats: [1, 4], load: false, traction: false, disp: false }
};
const OPS = ['SALE', 'RENT', 'WORK_RENT'];
const OP_NAMES = { SALE: 'Venta', RENT: 'Alquiler', WORK_RENT: 'Alquiler para trabajar' };

export function initWizard() {
    const root = document.querySelector('[data-wizard]');
    if (!root) return;

    const form = root.querySelector('#wizard-form');
    form.addEventListener('submit', e => e.preventDefault());
    const steps = [...root.querySelectorAll('[data-step]')];
    const markers = [...root.querySelectorAll('[data-stepper]')];
    const errorBox = root.querySelector('[data-wizard-error]');
    const btnPrev = root.querySelector('[data-prev]');
    const btnNext = root.querySelector('[data-next]');
    const btnDraft = root.querySelector('[data-draft]');
    const btnSubmit = root.querySelector('[data-submit]');
    const editable = root.dataset.editable === 'true';

    const state = {
        step: Math.min(Math.max(+root.dataset.startStep || 1, 1), 5),
        vehicleId: root.dataset.vehicleId || null,
        listingId: root.dataset.listingId || null,
        maxPhotos: +root.dataset.maxPhotos,
        activeUsed: +root.dataset.activeUsed,
        activeMax: +root.dataset.activeMax,
        images: JSON.parse(root.dataset.images || '[]'),
        busy: false
    };
    const q = sel => form.querySelector(sel);
    const typeCode = () => q('#vehicleTypeId').selectedOptions[0]?.dataset.code ?? '';

    // ---------------------------------------------------------------- navegación
    function reachable() { return state.listingId ? 5 : state.vehicleId ? 3 : 1; }

    function show(n) {
        state.step = n;
        steps.forEach((s, i) => { s.hidden = i + 1 !== n; });
        markers.forEach((m, i) => {
            m.classList.toggle('is-active', i + 1 === n);
            m.classList.toggle('is-done', i + 1 < n);
            m.setAttribute('aria-current', i + 1 === n ? 'step' : 'false');
        });
        btnPrev.hidden = n === 1;
        btnNext.hidden = n === 5 || (!editable && n < 5);
        btnSubmit.hidden = n !== 5 || !editable;
        btnDraft.hidden = n < 2 || !editable || n === 5;
        if (n === 3) fillTitle();
        if (n === 5) { renderPhotos(); renderSummary(); }
        errorBox.hidden = true;
        steps[n - 1].querySelector('legend')?.setAttribute('tabindex', '-1');
        steps[n - 1].querySelector('legend')?.focus({ preventScroll: true });
        root.scrollIntoView({ behavior: 'smooth', block: 'start' });
        initIcons();
    }

    markers.forEach((m, i) => { m.tabIndex = 0; m.addEventListener('click', () => { if (i + 1 <= reachable() && i + 1 !== state.step) show(i + 1); });
        m.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); m.click(); } }); });
    btnPrev.addEventListener('click', () => show(state.step - 1));

    // ---------------------------------------------------------------- reglas dinámicas por tipo / modalidad
    const wrap = name => q(`[data-field-wrap="${name}"]`);

    function applyTypeRules() {
        const code = typeCode();
        const rule = TYPE_RULES[code];
        const hint = q('[data-type-hint]');
        const fuelElectric = q('#fuelType').value === 'ELECTRIC';
        const doors = q('#doors'), seats = q('#seats'), load = q('#loadCapacityKg'), disp = q('#engineDisplacementCc');

        wrap('doors').hidden = !rule || !rule.doors;
        wrap('traction').hidden = !!rule && !rule.traction;
        wrap('loadCapacityKg').hidden = !rule || !rule.load;
        wrap('engineDisplacementCc').hidden = !!rule && !rule.disp || fuelElectric;

        doors.required = !!rule?.doors;
        if (rule?.doors) { doors.min = rule.doors[0]; doors.max = rule.doors[1]; }
        seats.required = !!rule;
        if (rule) { seats.min = rule.seats[0]; seats.max = rule.seats[1]; }
        load.required = rule?.load === true;
        q('[data-req="loadCapacityKg"]').hidden = rule?.load !== true;
        disp.required = !!rule?.disp && !fuelElectric;
        q('[data-req="engineDisplacementCc"]').hidden = !disp.required;

        const notes = [];
        if (rule?.doors) notes.push(`Puertas: ${rule.doors[0]} a ${rule.doors[1]}`);
        if (rule) notes.push(`Asientos: ${rule.seats[0]} a ${rule.seats[1]}`);
        if (rule?.load === true) notes.push('La capacidad de carga es obligatoria');
        hint.hidden = notes.length === 0;
        hint.querySelector('div').textContent = notes.join(' · ') + '.';

        const isNew = q('#condition').value === 'NEW';
        q('#plate').required = !isNew && !!q('#condition').value;
        q('[data-plate-label]').textContent = isNew ? 'Placa (opcional en vehículos nuevos)' : 'Placa *';
    }
    ['#vehicleTypeId', '#fuelType', '#condition'].forEach(s => q(s).addEventListener('change', applyTypeRules));

    // El año del modelo acompaña al de fabricación mientras el usuario no lo toque
    q('#manufactureYear').addEventListener('input', e => { const m = q('#modelYear'); if (!m.dataset.touched) m.value = e.target.value; });
    q('#modelYear').addEventListener('input', e => { e.target.dataset.touched = '1'; });
    q('#plate').addEventListener('input', e => { e.target.value = e.target.value.toUpperCase(); });

    // Modalidades
    const toggles = [...form.querySelectorAll('[data-op-toggle]')];
    const blocks = Object.fromEntries(OPS.map(o => [o, q(`[data-op-block="${o}"]`)]));
    function syncOps() { toggles.forEach(t => { blocks[t.dataset.opToggle].hidden = !t.checked; }); }
    toggles.forEach(t => t.addEventListener('change', () => { syncOps(); errorBox.hidden = true; }));
    const selectedOps = () => OPS.filter(o => toggles.find(t => t.dataset.opToggle === o)?.checked);

    // Anunciante empresarial / propietario legal
    const advType = q('#advertiserType');
    const syncAdv = () => { q('[data-business-wrap]').hidden = advType.value !== 'BUSINESS'; };
    advType.addEventListener('change', syncAdv);
    const legal = q('#legalOther');
    const syncLegal = () => { q('[data-legal-wrap]').hidden = !legal.checked; };
    legal.addEventListener('change', syncLegal);

    function fillTitle() {
        const t = q('#title');
        if (t.value.trim()) return;
        const brand = q('#brandId').selectedOptions[0]?.text, model = q('#modelId').selectedOptions[0]?.text;
        if (brand && model && q('#modelId').value) t.value = `${brand} ${model} ${q('#modelYear').value} ${q('#version').value}`.trim().replace(/\s+/g, ' ');
    }

    // ---------------------------------------------------------------- validación en el navegador
    function clearErrors(scope = form) {
        scope.querySelectorAll('.field-error').forEach(n => n.remove());
        scope.querySelectorAll('.is-invalid').forEach(n => { n.classList.remove('is-invalid'); n.removeAttribute('aria-invalid'); });
        errorBox.hidden = true;
    }

    function setError(input, message) {
        input.classList.add('is-invalid');
        input.setAttribute('aria-invalid', 'true');
        const host = input.closest('.form-check') ?? input;
        const msg = el('div', { class: 'field-error', role: 'alert', text: message });
        host.insertAdjacentElement('afterend', msg);
    }

    const visible = i => !i.closest('[hidden]') && !i.disabled;

    function validateFields(scope) {
        let first = null;
        scope.querySelectorAll('input, select, textarea').forEach(i => {
            if (!visible(i) || i.type === 'file' || i.type === 'checkbox') return;
            const v = i.value.trim();
            let msg = null;
            if (i.required && v === '') msg = 'Este campo es obligatorio.';
            else if (v !== '' && i.type === 'number') {
                const n = Number(v);
                if (Number.isNaN(n)) msg = 'Ingresa un número válido.';
                else if (i.min !== '' && n < Number(i.min)) msg = `El valor mínimo es ${i.min}.`;
                else if (i.max !== '' && n > Number(i.max)) msg = `El valor máximo es ${i.max}.`;
            } else if (v !== '' && i.minLength > 0 && v.length < i.minLength) msg = `Escribe al menos ${i.minLength} caracteres.`;
            if (msg) { setError(i, msg); first ??= i; }
        });
        return first;
    }

    function validateStep(n) {
        clearErrors(steps[n - 1]);
        let first = validateFields(steps[n - 1]);

        if (n === 1) {
            const my = +q('#manufactureYear').value, md = +q('#modelYear').value;
            if (my && md && (md < my || md > my + 1)) { setError(q('#modelYear'), 'Debe ser igual al año de fabricación o el siguiente.'); first ??= q('#modelYear'); }
        }
        if (n === 3) {
            if (selectedOps().length === 0) { showBanner('Selecciona al menos una modalidad: venta, alquiler o alquiler para trabajar.'); first ??= toggles[0]; }
            for (const op of selectedOps()) {
                const b = blocks[op];
                if (op === 'SALE') continue;
                const rates = ['dailyRate', 'weeklyRate', 'monthlyRate'].map(f => b.querySelector(`[data-op-field="${f}"]`));
                if (rates.every(r => r.value.trim() === '')) { setError(rates[0], 'Indica al menos una tarifa (diaria, semanal o mensual).'); first ??= rates[0]; }
            }
            const sale = blocks.SALE.querySelector('[data-op-field="salePrice"]');
            if (selectedOps().includes('SALE') && !(+sale.value > 0)) { if (!sale.classList.contains('is-invalid')) setError(sale, 'Ingresa el precio de venta.'); first ??= sale; }
        }
        if (n === 4) {
            if (!q('#phoneNumber').value.trim() && !q('#whatsAppNumber').value.trim()) { setError(q('#phoneNumber'), 'Indica un teléfono o un WhatsApp de contacto.'); first ??= q('#phoneNumber'); }
            if (advType.value === 'BUSINESS' && !q('#businessName').value.trim()) { setError(q('#businessName'), 'El nombre comercial es obligatorio.'); first ??= q('#businessName'); }
            if (legal.checked && !q('#legalOwnerName').value.trim()) { setError(q('#legalOwnerName'), 'Indica el nombre del propietario legal.'); first ??= q('#legalOwnerName'); }
            for (const sel of ['#phoneNumber', '#whatsAppNumber']) {
                const v = q(sel).value.replace(/[\s-]/g, '');
                if (v && !/^\+?\d{7,15}$/.test(v) && !q(sel).classList.contains('is-invalid')) { setError(q(sel), 'Número inválido (7 a 15 dígitos).'); first ??= q(sel); }
            }
        }
        if (first) { first.focus(); return false; }
        return true;
    }

    function showBanner(message) { errorBox.textContent = message; errorBox.hidden = false; }

    // ---------------------------------------------------------------- recopilación de datos
    const num = v => (v === '' || v == null ? null : Number(v));
    const txt = v => { const t = (v ?? '').trim(); return t === '' ? null : t; };

    function collectVehicle() {
        const body = {};
        form.querySelectorAll('[data-field="vehicle"]').forEach(i => {
            if (i.type === 'checkbox') body[i.name] = i.checked;
            else if (i.type === 'number' || i.tagName === 'SELECT' && /Id$/.test(i.name)) body[i.name] = num(i.value);
            else body[i.name] = txt(i.value);
        });
        if (!legal.checked) { body.legalOwnerName = null; body.legalOwnerRelationship = null; }
        // Los campos ocultos por reglas del tipo no se envían
        if (wrap('doors').hidden) body.doors = null;
        if (wrap('traction').hidden) body.traction = null;
        if (wrap('loadCapacityKg').hidden) body.loadCapacityKg = null;
        if (wrap('engineDisplacementCc').hidden) body.engineDisplacementCc = null;
        return body;
    }

    function collectListing() {
        const body = { vehicleId: state.vehicleId, operations: [] };
        form.querySelectorAll('[data-field="listing"]').forEach(i => {
            body[i.name] = i.type === 'number' || /Id$/.test(i.name) ? num(i.value) : txt(i.value);
        });
        if (body.advertiserType !== 'BUSINESS') body.businessName = null;
        body.title = body.title ?? '';
        for (const op of selectedOps()) {
            const b = blocks[op];
            const f = name => b.querySelector(`[data-op-field="${name}"]`);
            const o = {
                operationType: op, currency: f('currency').value,
                salePrice: op === 'SALE' ? num(f('salePrice').value) : null,
                dailyRate: op === 'SALE' ? null : num(f('dailyRate').value),
                weeklyRate: op === 'SALE' ? null : num(f('weeklyRate').value),
                monthlyRate: op === 'SALE' ? null : num(f('monthlyRate').value),
                guarantee: op === 'SALE' ? null : num(f('guarantee')?.value),
                isNegotiable: f('isNegotiable').checked,
                availableFrom: f('availableFrom').value ? `${f('availableFrom').value}T00:00:00Z` : null,
                additionalConditions: txt(f('additionalConditions').value)
            };
            if (op === 'WORK_RENT') {
                const w = {};
                b.querySelectorAll('[data-work-field]').forEach(i => { w[i.dataset.workField] = i.type === 'number' ? num(i.value) : (i.tagName === 'SELECT' ? i.value : txt(i.value)); });
                w.minExperienceYears ??= 0;
                o.workRent = w;
            }
            body.operations.push(o);
        }
        return body;
    }

    // ---------------------------------------------------------------- errores del servidor
    function showServerErrors(data) {
        const errors = data?.errors ?? {};
        const orphan = [];
        const ops = selectedOps();
        for (const [key, messages] of Object.entries(errors)) {
            const msg = messages.join(' ');
            let input = null;
            const m = /^Operations\[(\d+)\]\.?(\w*)$/.exec(key);
            if (m) {
                const op = ops[+m[1]];
                const field = m[2] ? m[2][0].toLowerCase() + m[2].slice(1) : null;
                input = op && field ? blocks[op].querySelector(`[data-op-field="${field}"], [data-work-field="${field}"]`) : null;
                if (!input && op) input = blocks[op].querySelector('[data-op-field="dailyRate"]');
            } else {
                const name = key[0].toLowerCase() + key.slice(1);
                input = form.querySelector(`[name="${name}"]`) ?? form.querySelector(`#${name}`);
            }
            if (input) { input.classList.contains('is-invalid') || setError(input, msg); }
            else orphan.push(msg);
        }
        const general = [data?.message && Object.keys(errors).length === 0 ? data.message : null, ...orphan].filter(Boolean);
        if (general.length) showBanner(general.join(' '));
        const bad = form.querySelector('.is-invalid');
        if (bad) {
            const stepEl = bad.closest('[data-step]');
            if (stepEl && +stepEl.dataset.step !== state.step) show(+stepEl.dataset.step);
            bad.focus();
        }
    }

    // ---------------------------------------------------------------- guardado
    function setBusy(b) {
        state.busy = b;
        [btnNext, btnPrev, btnDraft, btnSubmit].forEach(x => { x.disabled = b; x.classList.toggle('is-loading', b && x === btnNext); });
    }

    async function saveVehicle() {
        const url = '/panel/publish/vehicle' + (state.vehicleId ? `?id=${state.vehicleId}` : '');
        const { ok, data } = await sendJson('POST', url, collectVehicle());
        if (!ok) { showServerErrors(data); return false; }
        state.vehicleId = data.id;
        root.dataset.vehicleId = data.id;
        history.replaceState(null, '', `/panel/publish/${data.id}`);
        return true;
    }

    async function saveListing() {
        const url = '/panel/publish/listing' + (state.listingId ? `?id=${state.listingId}` : '');
        const { ok, data } = await sendJson('POST', url, collectListing());
        if (!ok) { showServerErrors(data); return false; }
        state.listingId = data.id;
        root.dataset.listingId = data.id;
        return true;
    }

    const stepsToValidate = n => Array.from({ length: n }, (_, i) => i + 1);

    async function advance() {
        if (state.busy) return;
        const n = state.step;
        if (!validateStep(n)) return;
        setBusy(true);
        try {
            if (n === 2) { if (!stepsToValidate(1).every(validateStep) || !(await saveVehicle())) return; toast('success', 'Vehículo guardado como borrador.'); }
            if (n === 4) {
                if (![1, 2, 3].every(validateStep)) return;
                if (!(await saveVehicle()) || !(await saveListing())) return;
                toast('success', 'Publicación guardada como borrador.');
            }
            show(n + 1);
        } finally { setBusy(false); }
    }

    async function saveDraftAndExit() {
        if (state.busy) return;
        const n = state.step;
        if (!stepsToValidate(Math.min(n, 4)).every(validateStep)) return;
        setBusy(true);
        try {
            if (!(await saveVehicle())) return;
            if (n >= 4 && !(await saveListing())) return;
            toast('success', 'Borrador guardado. Puedes continuar cuando quieras.');
            setTimeout(() => location.assign(n >= 4 ? '/panel/listings' : '/panel/vehicles'), 700);
        } finally { setBusy(false); }
    }

    btnNext.addEventListener('click', advance);
    btnDraft.addEventListener('click', saveDraftAndExit);
    form.addEventListener('keydown', e => { if (e.key === 'Enter' && e.target.tagName === 'INPUT' && e.target.type !== 'file' && e.target.type !== 'checkbox') { e.preventDefault(); if (state.step < 5) advance(); } });

    // ---------------------------------------------------------------- fotografías
    const grid = q('[data-photo-grid]');
    const emptyMsg = q('[data-photo-empty]');
    const drop = q('[data-dropzone]');
    const input = q('[data-photo-input]');

    function renderPhotos() {
        state.images.sort((a, b) => a.sortOrder - b.sortOrder);
        grid.replaceChildren(...state.images.map((img, i) => {
            const li = el('li', { class: `photo-tile ${img.isCover ? 'is-cover' : ''}` },
                el('img', { src: img.url, alt: `Fotografía ${i + 1}${img.isCover ? ' (portada)' : ''}`, width: 320, height: 240, loading: 'lazy' }),
                img.isCover ? el('span', { class: 'ml-badge ml-badge--info', text: 'Portada' }) : null,
                el('div', { class: 'photo-tile__bar' },
                    iconBtn('chevron-left', 'Mover a la izquierda', () => move(i, -1), i === 0),
                    iconBtn('star', img.isCover ? 'Ya es la portada' : 'Usar como portada', () => cover(img), img.isCover),
                    iconBtn('chevron-right', 'Mover a la derecha', () => move(i, 1), i === state.images.length - 1),
                    iconBtn('trash-2', 'Eliminar fotografía', () => remove(img))));
            return li;
        }));
        emptyMsg.hidden = state.images.length > 0;
        q('[data-photo-count]').textContent = state.images.length;
        initIcons();
        renderSummary();
    }

    function iconBtn(icon, label, onclick, disabled) {
        const b = el('button', { type: 'button', class: 'icon-btn', 'aria-label': label, title: label }, el('i', { 'data-lucide': icon }));
        if (disabled) b.disabled = true;
        b.addEventListener('click', onclick);
        return b;
    }

    const photoUrl = p => `/panel/publish/${state.vehicleId}/photos${p ?? ''}`;

    async function upload(files) {
        const allowed = ['image/jpeg', 'image/png', 'image/webp'];
        for (const file of files) {
            if (state.images.length >= state.maxPhotos) { toast('warning', `Tu plan permite ${state.maxPhotos} fotografías por vehículo.`); break; }
            if (!allowed.includes(file.type)) { toast('error', `«${file.name}»: solo se aceptan JPEG, PNG o WebP.`); continue; }
            if (file.size > 5 * 1024 * 1024) { toast('error', `«${file.name}»: supera el máximo de 5 MB.`); continue; }
            drop.classList.add('is-over');
            const fd = new FormData(); fd.append('file', file);
            const { ok, data } = await sendForm('POST', photoUrl(), fd);
            drop.classList.remove('is-over');
            if (!ok) { toast('error', data?.message ?? `No se pudo subir «${file.name}».`); if (data?.code === 'MAX_PHOTOS_PER_VEHICLE') break; continue; }
            state.images.push(data.image);
            renderPhotos();
        }
    }

    async function sync(res) { if (res.ok) { state.images = res.data.images; renderPhotos(); } else toast('error', res.data?.message ?? 'No se pudo actualizar las fotografías.'); }
    async function move(i, delta) {
        const ids = state.images.map(x => x.id); const j = i + delta;
        if (j < 0 || j >= ids.length) return;
        [ids[i], ids[j]] = [ids[j], ids[i]];
        await sync(await sendJson('PUT', photoUrl('/order'), { imageIds: ids }));
    }
    async function cover(img) { await sync(await sendJson('PATCH', photoUrl(`/${img.id}/cover`))); toast('success', 'Portada actualizada.'); }
    async function remove(img) {
        if (!(await confirmAction({ title: 'Eliminar fotografía', text: 'La fotografía se eliminará de este vehículo.', ok: 'Eliminar', variant: 'danger' }))) return;
        const res = await sendJson('DELETE', photoUrl(`/${img.id}`));
        if (res.ok) { state.images = res.data.images; renderPhotos(); toast('success', 'Fotografía eliminada.'); } else toast('error', res.data?.message ?? 'No se pudo eliminar.');
    }

    input.addEventListener('change', async () => { const files = [...input.files]; input.value = ''; await upload(files); });
    ['dragenter', 'dragover'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.add('is-over'); }));
    ['dragleave', 'drop'].forEach(t => drop.addEventListener(t, e => { e.preventDefault(); drop.classList.remove('is-over'); }));
    drop.addEventListener('drop', e => upload([...e.dataTransfer.files]));

    // ---------------------------------------------------------------- resumen y envío
    function renderSummary() {
        const ul = q('[data-summary]');
        const brand = q('#brandId').selectedOptions[0]?.text ?? '', model = q('#modelId').selectedOptions[0]?.text ?? '';
        const row = (ok, text) => el('li', { class: ok ? '' : 'text-muted' }, el('i', { 'data-lucide': ok ? 'check-circle-2' : 'circle', class: `icon ${ok ? 'yes' : 'no'}` }), text);
        ul.replaceChildren(
            row(!!state.vehicleId, `Vehículo: ${brand} ${model} ${q('#modelYear').value}`.trim()),
            row(!!state.listingId, `Modalidades: ${selectedOps().map(o => OP_NAMES[o]).join(', ') || 'sin definir'}`),
            row(state.images.length > 0, `Fotografías: ${state.images.length} de ${state.maxPhotos}`),
            row(!!(q('#phoneNumber').value || q('#whatsAppNumber').value), 'Contacto del anunciante'));
        initIcons();
    }

    btnSubmit.addEventListener('click', async () => {
        if (state.busy) return;
        const notice = q('[data-limit-notice]');
        notice.hidden = true;
        if (!state.listingId) { showBanner('Completa los pasos anteriores antes de enviar la publicación.'); return; }
        if (state.images.length === 0) { showBanner('Sube al menos una fotografía para enviar la publicación a revisión.'); return; }
        setBusy(true);
        const { ok, data, status } = await sendJson('POST', '/panel/publish/submit', { listingId: state.listingId });
        setBusy(false);
        if (ok) { toast('success', 'Publicación enviada a revisión. Te avisaremos cuando sea aprobada.'); setTimeout(() => location.assign('/panel/listings'), 900); return; }
        if (status === 403) { notice.querySelector('div').textContent = data?.message ?? 'Tu plan no permite esta acción.'; notice.hidden = false; return; }
        showServerErrors(data);
    });

    // ---------------------------------------------------------------- arranque
    applyTypeRules(); syncOps(); syncAdv(); syncLegal();
    if (!editable) form.querySelectorAll('[data-step]:not([data-step="5"])').forEach(s => { s.disabled = true; });
    show(state.step);
}
