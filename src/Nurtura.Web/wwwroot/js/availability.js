// "My availability" week grid: day tabs on a phone (one day shown at a time) and a live count of
// the open hours. Opening and closing an hour is a plain checkbox, so it needs no script.
(function () {
    var form = document.getElementById('availability-form');
    if (!form) {
        return;
    }

    var tabs = form.querySelectorAll('[data-day-tab]');
    var days = form.querySelectorAll('[data-day]');
    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            var index = tab.getAttribute('data-day-tab');
            tabs.forEach(function (other) {
                var isActive = other === tab;
                other.classList.toggle('btn-primary', isActive);
                other.classList.toggle('btn-outline-primary', !isActive);
                other.setAttribute('aria-pressed', isActive ? 'true' : 'false');
            });
            // d-none hides a day on a phone only; d-md-block shows every day from md up.
            days.forEach(function (day) {
                var isActive = day.getAttribute('data-day') === index;
                day.classList.toggle('d-none', !isActive);
                day.classList.toggle('d-md-block', !isActive);
            });
        });
    });

    var count = document.getElementById('availability-count');
    var template = count ? count.getAttribute('data-template') : null;
    function updateCount() {
        if (count && template) {
            count.textContent = template.replace('{0}', form.querySelectorAll('input[name="open"]:checked').length);
        }
    }

    form.addEventListener('change', updateCount);
    // Coming back with the browser's Back button can restore other checkboxes than the page was drawn with.
    window.addEventListener('pageshow', updateCount);
})();
