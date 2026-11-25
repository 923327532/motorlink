// Selects en cascada alimentados por la API (vía /catalogos/*). Ejemplo:
//   <select id="brandId" data-cascade='{"target":"#modelId","url":"/catalogos/modelos","params":{"brandId":"#brandId"},"require":["brandId"],"placeholder":"Todos"}'>
// El destino puede declarar data-selected="<id>" para conservar su valor al cargar la página.
const qs = s => document.querySelector(s);

async function load(cfg) {
    const target = qs(cfg.target);
    if (!target) return;
    const params = new URLSearchParams();
    for (const [name, sel] of Object.entries(cfg.params ?? {})) {
        const v = qs(sel)?.value;
        if (v) params.set(name, v);
    }
    const missing = (cfg.require ?? []).some(r => !params.get(r));
    const keep = target.dataset.selected;
    target.replaceChildren(new Option(cfg.placeholder ?? 'Seleccionar', ''));
    target.disabled = missing;
    if (missing) { target.dispatchEvent(new CustomEvent('cascade:cleared')); return; }

    target.setAttribute('aria-busy', 'true');
    try {
        const res = await fetch(`${cfg.url}?${params}`, { headers: { Accept: 'application/json' } });
        const items = res.ok ? await res.json() : [];
        for (const it of items) target.append(new Option(it.name, it.id, false, String(it.id) === keep));
    } finally {
        target.removeAttribute('aria-busy');
        delete target.dataset.selected;
    }
    target.dispatchEvent(new CustomEvent('cascade:loaded'));
}

export function initCascades() {
    document.querySelectorAll('[data-cascade]').forEach(src => {
        const cfg = JSON.parse(src.dataset.cascade);
        const triggers = new Set([src, ...Object.values(cfg.params ?? {}).map(s => qs(s)).filter(Boolean)]);
        triggers.forEach(t => t.addEventListener('change', () => load(cfg)));
        // Si el destino se vacía, sus propios dependientes también se reinician
        qs(cfg.target)?.addEventListener('cascade:cleared', () => qs(cfg.target).dispatchEvent(new Event('change')));
        if (src.dataset.cascadeInit !== 'false') load(cfg);
    });
}
