// Shows "n / max characters" under every <textarea data-note-input>, in the <div data-note-counter>
// that follows it in the same parent. The text comes from data-template ({0} = length, {1} = max).
(function () {
    document.querySelectorAll('[data-note-input]').forEach(function (note) {
        var counter = note.parentElement.querySelector('[data-note-counter]');
        if (!counter) {
            return;
        }

        function updateCounter() {
            counter.textContent = counter.dataset.template
                .replace('{0}', note.value.length)
                .replace('{1}', counter.dataset.max);
        }

        note.addEventListener('input', updateCounter);
        updateCounter();
    });
})();
