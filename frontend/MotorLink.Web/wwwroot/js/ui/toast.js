// Sistema reutilizable de notificaciones visuales (reemplaza a alert()).
import { el } from '../core/dom.js';
import { initIcons } from './icons.js';

const ICONS = { success: 'check-circle-2', error: 'circle-alert', info: 'info', warning: 'triangle-alert' };
const TITLES = { success: 'Listo', error: 'Error', info: 'Aviso', warning: 'Atención' };

export function toast(type, message, { timeout } = {}) {
    const stack = document.getElementById('toast-stack');
    if (!stack || !message) return;
    const close = el('button', { class: 'ml-toast__close', type: 'button', 'aria-label': 'Cerrar notificación' }, el('i', { 'data-lucide': 'x' }));
    const node = el('div', { class: `ml-toast ml-toast--${type}`, role: type === 'error' ? 'alert' : 'status' },
        el('span', { class: 'ml-toast__icon' }, el('i', { 'data-lucide': ICONS[type] ?? 'info' })),
        el('div', { class: 'ml-toast__body' }, el('strong', { text: TITLES[type] ?? 'Aviso' }), el('div', { text: message })),
        close);
    close.addEventListener('click', () => node.remove());
    stack.append(node);
    initIcons();
    setTimeout(() => node.remove(), timeout ?? (type === 'error' ? 9000 : 5500));
}

export function initToasts() {
    document.querySelectorAll('[data-toast]').forEach(n => { toast(n.dataset.type, n.dataset.message); n.remove(); });
}
