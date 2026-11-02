// Draws the four rating charts on the mother dashboard from the JSON in #wellbeing-chart-data.
// One calm color, y-axis always 1 to 5, no zones. Days without a check-in stay empty: the line is solid
// between check-ins on following days and faint and dotted across missing days.
(function () {
    var dataElement = document.getElementById('wellbeing-chart-data');
    if (typeof Chart === 'undefined' || !dataElement) {
        return;
    }

    var data = JSON.parse(dataElement.textContent);
    var color = getComputedStyle(document.body).getPropertyValue('--bs-primary').trim() || '#0d6efd';
    var faintColor = color;
    var hex = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i.exec(color);
    if (hex) {
        faintColor = 'rgba(' + parseInt(hex[1], 16) + ',' + parseInt(hex[2], 16) + ',' + parseInt(hex[3], 16) + ',0.4)';
    }

    // With spanGaps, Chart.js draws the gap through the skipped (null) points, so a segment that
    // touches one is part of a gap. The index check is a fallback for segments drawn straight across.
    function acrossMissingDays(ctx) {
        return ctx.p0.skip || ctx.p1.skip || ctx.p1DataIndex - ctx.p0DataIndex > 1;
    }

    document.querySelectorAll('canvas[data-rating]').forEach(function (canvas) {
        new Chart(canvas, {
            type: 'line',
            data: {
                labels: data.labels,
                datasets: [{
                    label: canvas.dataset.label,
                    data: data.series[canvas.dataset.rating],
                    borderColor: color,
                    backgroundColor: color,
                    borderWidth: 2,
                    pointRadius: 3,
                    spanGaps: true,
                    segment: {
                        borderColor: function (ctx) { return acrossMissingDays(ctx) ? faintColor : undefined; },
                        borderDash: function (ctx) { return acrossMissingDays(ctx) ? [2, 4] : undefined; },
                        borderWidth: function (ctx) { return acrossMissingDays(ctx) ? 1 : undefined; }
                    }
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: function (items) { return data.dates[items[0].dataIndex]; }
                        }
                    }
                },
                scales: {
                    y: { min: 1, max: 5, ticks: { stepSize: 1 } },
                    x: { ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 7 } }
                }
            }
        });
    });
})();
