// Galería: foto principal, miniaturas, flechas y teclado.
export function initGallery() {
    document.querySelectorAll('[data-gallery]').forEach(g => {
        const main = g.querySelector('[data-gallery-main]');
        const thumbs = [...g.querySelectorAll('[data-gallery-thumb]')];
        const counter = g.querySelector('[data-gallery-count]');
        if (!main || thumbs.length === 0) return;
        let i = 0;
        const show = n => {
            i = (n + thumbs.length) % thumbs.length;
            main.src = thumbs[i].dataset.full;
            main.alt = thumbs[i].dataset.alt;
            thumbs.forEach((t, k) => t.setAttribute('aria-current', String(k === i)));
            if (counter) counter.textContent = `${i + 1} / ${thumbs.length}`;
        };
        thumbs.forEach((t, k) => t.addEventListener('click', () => show(k)));
        g.querySelector('[data-gallery-prev]')?.addEventListener('click', () => show(i - 1));
        g.querySelector('[data-gallery-next]')?.addEventListener('click', () => show(i + 1));
        g.addEventListener('keydown', e => {
            if (e.key === 'ArrowLeft') show(i - 1);
            if (e.key === 'ArrowRight') show(i + 1);
        });
    });
}
