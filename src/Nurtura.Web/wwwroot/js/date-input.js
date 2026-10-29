// Every <input data-date-input> gets a flatpickr calendar in dd-mm-yyyy (see Localization/DateFormats.cs).
// Optional data-date-min / data-date-max take dd-mm-yyyy. Typing the date by hand still works.
// Not data-min-date / data-max-date: flatpickr reads those attributes itself and parses them as
// yyyy-mm-dd, which turned 29-10-2026 into 01-01-2026.
(function () {
    if (typeof flatpickr === 'undefined') {
        return;
    }

    var isPolish = document.documentElement.lang.toLowerCase().indexOf('pl') === 0;

    document.querySelectorAll('input[data-date-input]').forEach(function (input) {
        flatpickr(input, {
            dateFormat: 'd-m-Y',
            allowInput: true,
            // Phones would otherwise get the browser's own date box, which ignores dd-mm-yyyy.
            disableMobile: true,
            locale: isPolish ? flatpickr.l10ns.pl : 'default',
            minDate: input.dataset.dateMin || null,
            maxDate: input.dataset.dateMax || null
        });
    });
})();
