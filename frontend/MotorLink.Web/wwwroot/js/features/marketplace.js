// Buscador: envío automático al cambiar modalidad/orden y reinicio de la página al filtrar.
export function initMarketplace() {
    document.querySelectorAll('[data-autosubmit]').forEach(n => n.addEventListener('change', () => {
        const form = n.form ?? document.querySelector(n.dataset.autosubmit);
        form?.querySelector('input[name="page"]')?.remove();
        form?.requestSubmit();
    }));

    // Los formularios de filtros (escritorio y móvil) comparten estado: se envía el que se use.
    document.querySelectorAll('form[data-filters] input[type="radio"]').forEach(r =>
        r.addEventListener('change', () => r.form.requestSubmit()));
}
