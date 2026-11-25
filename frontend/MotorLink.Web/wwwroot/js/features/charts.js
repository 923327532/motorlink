// Gráficos con Chart.js. Configuración declarativa en el marcado:
//   <canvas data-chart='{"type":"line","labels":["2026-10"],"datasets":[{"label":"Visualizaciones","data":[10]}]}' data-label="descripción accesible">
const PALETTE = ['#2563EB', '#0F172A', '#16A34A', '#D97706', '#7C3AED', '#64748B'];

export function initCharts() {
    if (!window.Chart) return;
    Chart.defaults.font.family = '"Inter", system-ui, sans-serif';
    Chart.defaults.color = '#64748B';

    document.querySelectorAll('canvas[data-chart]').forEach(canvas => {
        const cfg = JSON.parse(canvas.dataset.chart);
        const round = cfg.type === 'doughnut';
        new Chart(canvas, {
            type: cfg.type,
            data: {
                labels: cfg.labels,
                datasets: cfg.datasets.map((d, i) => ({
                    borderWidth: 2,
                    borderColor: round ? '#fff' : PALETTE[i % PALETTE.length],
                    backgroundColor: round ? cfg.labels.map((_, k) => PALETTE[k % PALETTE.length]) : PALETTE[i % PALETTE.length],
                    borderRadius: cfg.type === 'bar' ? 6 : 0,
                    tension: .3, fill: false, pointRadius: 3,
                    ...d
                }))
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: cfg.horizontal ? 'y' : 'x',
                plugins: { legend: { display: round || cfg.datasets.length > 1, position: 'bottom', labels: { boxWidth: 10, usePointStyle: true } } },
                scales: round ? {} : {
                    y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#E2E8F0' } },
                    x: { grid: { display: false } }
                }
            }
        });
        canvas.setAttribute('role', 'img');
        if (canvas.dataset.label) canvas.setAttribute('aria-label', canvas.dataset.label);
    });
}
