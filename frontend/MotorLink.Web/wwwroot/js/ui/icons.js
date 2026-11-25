// Lucide: convierte <i data-lucide="..."> en SVG. Se puede volver a invocar tras insertar HTML dinámico.
export function initIcons() {
    if (window.lucide) window.lucide.createIcons({ attrs: { 'aria-hidden': 'true', focusable: 'false' } });
}
