// Botón de favorito (persistente en la API). Requiere sesión.
import { sendJson, isAuthenticated, loginUrl } from '../core/http.js';
import { toast } from '../ui/toast.js';

export function initFavorites() {
    document.addEventListener('click', async e => {
        const b = e.target.closest('[data-favorite]');
        if (!b) return;
        e.preventDefault(); e.stopPropagation();
        if (!isAuthenticated()) { location.assign(loginUrl()); return; }
        if (b.disabled) return;
        const id = b.dataset.favorite;
        const wasOn = b.getAttribute('aria-pressed') === 'true';
        b.disabled = true;
        const { ok, data } = await sendJson(wasOn ? 'DELETE' : 'POST', `/vehiculos/${id}/favorito`);
        b.disabled = false;
        if (!ok) { if (data?.message) toast('error', data.message); return; }
        b.setAttribute('aria-pressed', String(!wasOn));
        b.setAttribute('aria-label', wasOn ? 'Guardar en favoritos' : 'Quitar de favoritos');
        const label = b.querySelector('[data-favorite-label]');
        if (label) label.textContent = wasOn ? 'Guardar favorito' : 'Guardado en favoritos';
        toast('success', wasOn ? 'Quitado de tus favoritos.' : 'Guardado en tus favoritos.');
        if (wasOn && b.dataset.removeCard) b.closest(b.dataset.removeCard)?.remove();
    });
}
