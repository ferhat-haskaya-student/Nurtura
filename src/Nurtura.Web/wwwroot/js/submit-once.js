// <form data-submit-once>: after one tap the form's submit buttons are disabled, so a double-tap
// does not log a feed or a sleep twice.
// Disabled in the next tick: the browser has then already taken the tapped button's name and value
// (the sleep screen's "command"), and a submit stopped by client validation (defaultPrevented) changes nothing.
(function () {
    document.querySelectorAll('form[data-submit-once]').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            setTimeout(function () {
                if (event.defaultPrevented) {
                    return;
                }
                form.querySelectorAll('button[type="submit"]').forEach(function (button) {
                    button.disabled = true;
                });
            }, 0);
        });
    });

    // Coming back with the browser's Back button can show the page as it was left: tappable again.
    window.addEventListener('pageshow', function () {
        document.querySelectorAll('form[data-submit-once] button[type="submit"]').forEach(function (button) {
            button.disabled = false;
        });
    });
})();
