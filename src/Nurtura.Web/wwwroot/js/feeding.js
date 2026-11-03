// The feeding screen (Areas/Mother/Views/Feeding/Log.cshtml): only the chosen type's fields show,
// and the minute buttons fill the minutes box. Without this script every type's fields show;
// the server uses only the chosen type's fields either way.
(function () {
    var typeInputs = document.querySelectorAll('input[name="Type"]');
    var groups = document.querySelectorAll('[data-feeding-fields]');
    var note = document.querySelector('[data-solids-placeholder]');

    function showChosen() {
        var chosen = document.querySelector('input[name="Type"]:checked');
        groups.forEach(function (group) {
            group.hidden = !chosen || group.dataset.feedingFields !== chosen.value;
        });
        // Solids: what the baby ate goes in the note.
        if (note) {
            note.placeholder = chosen && chosen.value === 'Solid' ? note.dataset.solidsPlaceholder : '';
        }
    }

    typeInputs.forEach(function (input) {
        input.addEventListener('change', showChosen);
    });
    showChosen();

    var minutesInput = document.querySelector('[data-minutes-input]');
    var minuteButtons = document.querySelectorAll('[data-minutes]');

    function markMinutes() {
        minuteButtons.forEach(function (button) {
            var pressed = button.dataset.minutes === minutesInput.value;
            button.setAttribute('aria-pressed', pressed ? 'true' : 'false');
            button.classList.toggle('active', pressed);
        });
    }

    minuteButtons.forEach(function (button) {
        button.addEventListener('click', function () {
            minutesInput.value = button.dataset.minutes;
            markMinutes();
        });
    });
    if (minutesInput) {
        minutesInput.addEventListener('input', markMinutes);
        markMinutes();
    }
})();
