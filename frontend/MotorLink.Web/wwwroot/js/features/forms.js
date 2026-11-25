// Comportamientos comunes de formularios.
export function initForms() {
    // Mostrar/ocultar contraseña
    document.addEventListener('click', e => {
        const b = e.target.closest('[data-toggle-password]');
        if (!b) return;
        const input = document.querySelector(b.dataset.togglePassword);
        if (!input) return;
        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        b.setAttribute('aria-label', show ? 'Ocultar contraseña' : 'Mostrar contraseña');
        b.setAttribute('aria-pressed', String(show));
        const icon = b.querySelector('svg, i');
        if (icon) {
            const fresh = document.createElement('i');
            fresh.setAttribute('data-lucide', show ? 'eye-off' : 'eye');
            icon.replaceWith(fresh);
            window.lucide?.createIcons({ attrs: { 'aria-hidden': 'true' } });
        }
    });

    // Campos de cuenta empresarial (registro): tarjetas de selección
    const radios = document.querySelectorAll('input[data-account-choice]');
    const biz = document.querySelector('[data-business-fields]');
    const sync = () => {
        if (!biz) return;
        const business = document.querySelector('input[data-account-choice]:checked')?.value === 'BUSINESS';
        biz.hidden = !business;
        biz.querySelectorAll('input').forEach(i => { i.disabled = !business; });
    };
    radios.forEach(r => r.addEventListener('change', sync));
    sync();

    // Selector de tipo de perfil (perfil de usuario)
    const type = document.querySelector('[data-profile-type]');
    if (type) {
        const toggle = () => document.querySelectorAll('[data-business-only]').forEach(n => { n.hidden = type.value !== 'BUSINESS'; });
        type.addEventListener('change', toggle); toggle();
    }
}
