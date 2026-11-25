// Estado de carga en formularios: evita dobles envíos y muestra un indicador en el botón.
export function initLoading() {
    document.addEventListener('submit', e => {
        const form = e.target;
        if (!(form instanceof HTMLFormElement) || form.hasAttribute('data-no-loading')) return;
        if (form.dataset.submitting === 'true') { e.preventDefault(); return; }
        // jQuery Validate cancela el evento si hay errores: se revisa al terminar el ciclo actual
        setTimeout(() => {
            if (e.defaultPrevented) return;
            form.dataset.submitting = 'true';
            form.querySelectorAll('button[type="submit"]:not([data-no-loading])').forEach(b => {
                b.classList.add('is-loading');
                if (!b.querySelector('.spinner')) { const s = document.createElement('span'); s.className = 'spinner'; s.setAttribute('aria-hidden', 'true'); b.prepend(s); }
            });
        }, 0);
    });
    // al volver con "atrás" el formulario no debe quedar bloqueado
    window.addEventListener('pageshow', e => {
        if (!e.persisted) return;
        document.querySelectorAll('form[data-submitting]').forEach(f => { delete f.dataset.submitting; f.querySelectorAll('.is-loading').forEach(b => b.classList.remove('is-loading')); });
    });
}
