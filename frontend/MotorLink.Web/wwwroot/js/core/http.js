// Utilidades HTTP: token antiforgery, JSON y manejo uniforme de 401/403 (nunca se guardan credenciales en el navegador).
import { toast } from '../ui/toast.js';

export const csrfToken = () => document.querySelector('#csrf-form input[name="__RequestVerificationToken"]')?.value ?? '';

const toLogin = () => setTimeout(() => location.assign(`/ingresar?returnUrl=${encodeURIComponent(location.pathname + location.search)}&expired=1`), 900);

async function request(url, init) {
    let res;
    try {
        res = await fetch(url, { credentials: 'same-origin', ...init });
    } catch {
        toast('error', 'No hay conexión con el servidor. Revisa tu red e inténtalo nuevamente.');
        return { ok: false, status: 0, data: null };
    }
    const data = await res.json().catch(() => null);
    if (res.status === 401) { toast('warning', 'Tu sesión expiró. Te llevamos a iniciar sesión.'); toLogin(); }
    else if (res.status === 403 && !data?.message) toast('error', 'No tienes permisos para realizar esta acción.');
    return { ok: res.ok, status: res.status, data };
}

/** Envía JSON a una acción MVC. Devuelve { ok, status, data }. Redirige al login si la sesión expiró. */
export const sendJson = (method, url, body) => request(url, {
    method,
    headers: { 'Content-Type': 'application/json', Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'X-CSRF-TOKEN': csrfToken() },
    body: body === undefined ? undefined : JSON.stringify(body)
});

export const sendForm = (method, url, formData) => request(url, {
    method,
    headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest', 'X-CSRF-TOKEN': csrfToken() },
    body: formData
});

export const isAuthenticated = () => document.body.dataset.auth === 'true';
export const loginUrl = () => `/ingresar?returnUrl=${encodeURIComponent(location.pathname + location.search)}`;
