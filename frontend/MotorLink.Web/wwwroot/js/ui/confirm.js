// Confirmaciones con modal de Bootstrap (sin confirm() del navegador). Uso:
//   <button type="submit" data-confirm="¿Eliminar este vehículo?" data-confirm-title="Eliminar" data-confirm-ok="Eliminar" data-confirm-variant="danger">
import { el } from '../core/dom.js';

let modal, titleEl, textEl, okBtn, rootEl, resolver;

function ensureModal() {
    if (modal) return;
    titleEl = el('h2', { class: 'modal-title', id: 'confirm-title' });
    textEl = el('p', { class: 'mb-0', id: 'confirm-text' });
    okBtn = el('button', { type: 'button', class: 'btn btn-primary' });
    rootEl = el('div', { class: 'modal fade', id: 'confirm-modal', tabindex: '-1', 'aria-labelledby': 'confirm-title', 'aria-describedby': 'confirm-text', 'aria-hidden': 'true' },
        el('div', { class: 'modal-dialog modal-dialog-centered' }, el('div', { class: 'modal-content' },
            el('div', { class: 'modal-header' }, titleEl, el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Cerrar' })),
            el('div', { class: 'modal-body' }, textEl),
            el('div', { class: 'modal-footer' }, el('button', { type: 'button', class: 'btn btn-light', 'data-bs-dismiss': 'modal', text: 'Cancelar' }), okBtn))));
    document.body.append(rootEl);
    modal = new bootstrap.Modal(rootEl);
    okBtn.addEventListener('click', () => { const r = resolver; resolver = null; r?.(true); modal.hide(); });
    rootEl.addEventListener('hidden.bs.modal', () => { const r = resolver; resolver = null; r?.(false); });
}

export function confirmAction({ title = 'Confirmar acción', text, ok = 'Confirmar', variant = 'primary' }) {
    ensureModal();
    titleEl.textContent = title; textEl.textContent = text;
    okBtn.textContent = ok; okBtn.className = `btn btn-${variant === 'danger' ? 'danger' : 'primary'}`;
    return new Promise(resolve => { resolver = resolve; modal.show(); });
}

export function initConfirm() {
    document.addEventListener('click', async e => {
        const t = e.target.closest('[data-confirm]');
        if (!t || t.dataset.confirmed === 'true') return;
        e.preventDefault(); e.stopPropagation();
        const ok = await confirmAction({ title: t.dataset.confirmTitle, text: t.dataset.confirm, ok: t.dataset.confirmOk, variant: t.dataset.confirmVariant });
        if (!ok) return;
        t.dataset.confirmed = 'true';
        if (t.form && t.tagName === 'BUTTON') t.form.requestSubmit(t); else t.click();
        setTimeout(() => delete t.dataset.confirmed, 0);
    }, true);
}
