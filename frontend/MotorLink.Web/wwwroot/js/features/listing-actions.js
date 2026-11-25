// Detalle de publicación: visualización, contacto (WhatsApp/teléfono) y formulario de propuesta.
import { sendJson, isAuthenticated, loginUrl } from '../core/http.js';
import { toast } from '../ui/toast.js';
import { money } from '../core/dom.js';

export function initListingActions() {
    const page = document.querySelector('[data-listing-page]');
    if (!page) return;
    const id = page.dataset.listingPage;

    // Registro de visualización (la API ignora al propio anunciante y las repeticiones en 30 min)
    sendJson('POST', `/vehiculos/${id}/vista`);

    document.addEventListener('click', async e => {
        const b = e.target.closest('[data-contact]');
        if (!b) return;
        e.preventDefault();
        const channel = b.dataset.contact;                                         // WHATSAPP | PHONE
        const win = channel === 'WHATSAPP' ? window.open('', '_blank') : null;     // se abre en el clic para evitar bloqueadores
        b.classList.add('is-loading');
        const { ok, data } = await sendJson('POST', `/vehiculos/${id}/contacto`, { channel });
        b.classList.remove('is-loading');
        if (!ok) { win?.close(); toast('error', data?.message ?? 'No se pudo obtener el contacto.'); return; }
        if (data.whatsAppUrl) { if (win) { win.opener = null; win.location = data.whatsAppUrl; } else location.assign(data.whatsAppUrl); }
        if (data.phoneNumber) {
            const out = document.querySelector('[data-phone-out]');
            if (out) { out.hidden = false; const a = out.querySelector('a'); a.textContent = data.phoneNumber; a.href = `tel:${data.phoneNumber}`; }
        }
        toast('info', data.disclaimer);
    });

    // Propuesta: modalidad → periodicidad y precio publicado como referencia
    const form = document.querySelector('[data-offer-form]');
    if (form) {
        const op = form.querySelector('[name="OperationType"]');
        const period = form.querySelector('[name="Periodicity"]');
        const periodWrap = form.querySelector('[data-period-wrap]');
        const hint = form.querySelector('[data-offer-hint]');
        const symbol = form.querySelector('[data-symbol]');
        const sync = () => {
            const o = op.selectedOptions[0];
            const rent = o.dataset.rent === 'true';
            periodWrap.hidden = !rent;
            period.disabled = !rent;
            if (rent) {
                [...period.options].forEach(p => { const has = !!o.dataset[p.value.toLowerCase()]; p.hidden = !has; p.disabled = !has; });
                if (!period.value || period.selectedOptions[0]?.disabled) period.value = [...period.options].find(p => !p.disabled)?.value ?? '';
            }
            symbol.textContent = o.dataset.symbol;
            const listed = rent ? o.dataset[period.value.toLowerCase()] : o.dataset.sale;
            hint.textContent = listed ? `Precio publicado: ${money(o.dataset.symbol, listed)}${rent ? ' ' + period.selectedOptions[0].text.toLowerCase() : ''}` : '';
        };
        op.addEventListener('change', sync);
        period.addEventListener('change', sync);
        sync();
    }

    document.querySelectorAll('[data-require-login]').forEach(b => b.addEventListener('click', e => {
        if (!isAuthenticated()) { e.preventDefault(); e.stopPropagation(); location.assign(loginUrl()); }
    }, true));
}
