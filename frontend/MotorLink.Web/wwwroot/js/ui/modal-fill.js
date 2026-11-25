// Rellena un modal con datos del botón que lo abre: data-fill='{"id":"..","name":".."}'
//  · inputs con name=<clave> reciben el valor; [data-fill-text="clave"] recibe texto; form[data-fill-action] reemplaza {id} en su URL.
export function initModalFill() {
    document.addEventListener('show.bs.modal', e => {
        const trigger = e.relatedTarget;
        if (!trigger?.dataset.fill) return;
        const data = JSON.parse(trigger.dataset.fill);
        const modal = e.target;
        for (const [k, v] of Object.entries(data)) {
            modal.querySelectorAll(`[name="${k}"]`).forEach(i => { if (i.type === 'checkbox') i.checked = Array.isArray(v) ? v.map(String).includes(i.value) : (v === true || v === 'true'); else i.value = v ?? ''; });
            modal.querySelectorAll(`[data-fill-text="${k}"]`).forEach(n => { n.textContent = v ?? ''; });
        }
        modal.querySelectorAll('form[data-fill-action]').forEach(f => { f.action = f.dataset.fillAction.replace('{id}', data.id); });
    });
}
