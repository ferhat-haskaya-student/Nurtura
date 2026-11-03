// Running sleep times tick in the browser, but the start always comes from the server
// (data-running-since, UTC milliseconds), so closing the phone loses nothing.
// data-server-now (UTC milliseconds when the page was made) corrects a phone clock that is off.
// data-format="clock" shows h:mm:ss; "duration" shows the localized data-template-hm / -h / -m.
(function () {
    var elements = document.querySelectorAll('[data-running-since]');
    if (elements.length === 0) {
        return;
    }

    var serverNow = Number(document.querySelector('[data-server-now]').dataset.serverNow);
    var clockOffset = serverNow - Date.now();

    function pad(n) {
        return n < 10 ? '0' + n : String(n);
    }

    function update() {
        var now = Date.now() + clockOffset;
        elements.forEach(function (element) {
            var seconds = Math.max(0, Math.floor((now - Number(element.dataset.runningSince)) / 1000));
            var hours = Math.floor(seconds / 3600);
            var minutes = Math.floor(seconds % 3600 / 60);
            if (element.dataset.format === 'clock') {
                element.textContent = hours + ':' + pad(minutes) + ':' + pad(seconds % 60);
            } else {
                // Same rule as SleepText.Duration on the server: "1 h 20 min", "3 h", "45 min".
                var template = hours === 0 ? element.dataset.templateM
                    : minutes === 0 ? element.dataset.templateH : element.dataset.templateHm;
                element.textContent = template.replace('{0}', hours > 0 ? hours : minutes).replace('{1}', minutes);
            }
        });
    }

    update();
    setInterval(update, 1000);
})();
