// Campana de notificaciones: contador periódico y listado al abrir el menú (datos reales de la API).
import { el } from '../core/dom.js';
import { sendJson } from '../core/http.js';
import { initIcons } from '../ui/icons.js';

export function initNotifications() {
    const bell = document.querySelector('[data-bell]');
    if (!bell) return;
    const dot = bell.querySelector('[data-bell-count]');
    const list = bell.querySelector('[data-bell-list]');
    const base = bell.dataset.bell;                       // /panel/notifications

    async function refreshCount() {
        const r = await fetch(`${base}/count`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' }).catch(() => null);
        if (!r?.ok) return;
        const { count } = await r.json();
        dot.hidden = count === 0;
        dot.textContent = count > 99 ? '99+' : count;
    }

    async function loadList() {
        list.replaceChildren(...[1, 2, 3].map(() => el('div', { class: 'p-3' }, el('div', { class: 'skeleton' }))));
        const r = await fetch(`${base}/latest`, { headers: { Accept: 'application/json' }, credentials: 'same-origin' }).catch(() => null);
        if (!r?.ok) { list.replaceChildren(el('div', { class: 'p-3 text-muted small', text: 'No se pudieron cargar las notificaciones.' })); return; }
        const items = await r.json();
        if (items.length === 0) { list.replaceChildren(el('div', { class: 'p-4 text-center text-muted small', text: 'No tienes notificaciones.' })); return; }
        list.replaceChildren(...items.map(n => el('a', { class: `bell-menu__item ${n.isRead ? '' : 'is-unread'}`, href: n.url },
            el('strong', { text: n.title }), el('span', { text: n.message }), el('small', { text: n.when }))));
        initIcons();
    }

    bell.addEventListener('show.bs.dropdown', loadList);
    bell.querySelector('[data-bell-readall]')?.addEventListener('click', async e => {
        e.preventDefault();
        await sendJson('POST', `${base}/read-all`);
        refreshCount();
        loadList();
    });
    refreshCount();
    setInterval(() => { if (document.visibilityState === 'visible') refreshCount(); }, 60000);
}
