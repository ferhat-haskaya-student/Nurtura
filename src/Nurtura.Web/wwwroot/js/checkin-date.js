// Picking another day opens that day's check-in.
(function () {
    var dateForm = document.querySelector('[data-checkin-date-form]');
    dateForm.querySelector('input[name="date"]').addEventListener('change', function () {
        dateForm.submit();
    });
})();
