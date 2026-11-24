// Running sleep times tick in the browser, but the start always comes from the server
// (data-running-since, UTC milliseconds), so closing the phone loses nothing.
// data-server-now (UTC milliseconds when the page was made) corrects a phone clock that is off.
// data-format="clock" shows h:mm:ss; "duration" shows the localized data-template-hm / -h / -m.
// A script that changes these attributes (sleep-timer-api.js) sends "sleep-timer:refresh" to the document.
//
// On the timer screen the clock follows the picked times before anything is saved: only a start shows the time since it,
// a start and an end show the length. Only a running sleep ticks, so the clock and the button agree. The attributes stay
// as the server gave them.
(function () {
    var MINUTE_MS = 60000;
    var HOUR_MS = 60 * MINUTE_MS;

    var ticking = [];
    var clockOffset = 0;
    var timer = null;

    function pad(n) {
        return n < 10 ? '0' + n : String(n);
    }

    function clockText(ms) {
        var seconds = Math.max(0, Math.floor(ms / 1000));
        return Math.floor(seconds / 3600) + ':' + pad(Math.floor(seconds % 3600 / 60)) + ':' + pad(seconds % 60);
    }

    // Same rule as SleepText.Duration on the server: "1 h 20 min", "3 h", "45 min".
    function durationText(ms, templates) {
        var total = Math.max(0, Math.floor(ms / MINUTE_MS));
        var hours = Math.floor(total / 60);
        var minutes = total % 60;
        var template = hours === 0 ? templates.templateM : minutes === 0 ? templates.templateH : templates.templateHm;
        return template.replace('{0}', hours > 0 ? hours : minutes).replace('{1}', minutes);
    }

    // The server's time.
    function now() {
        return Date.now() + clockOffset;
    }

    function update() {
        ticking.forEach(function (item) {
            var element = item.element;
            var ms = now() - item.since;
            element.textContent = item.format === 'clock' ? clockText(ms) : durationText(ms, element.dataset);
        });
    }

    // Ticks only while something on the page runs.
    function run() {
        clearInterval(timer);
        if (ticking.length > 0) {
            update();
            timer = setInterval(update, 1000);
        }
    }

    // --- The timer screen: the clock follows the fields ---------------------------------------

    var form = document.querySelector('[data-sleep-form]');
    var clock = form && form.querySelector('[data-sleep-clock]');
    var startField = form && document.getElementById('start');
    var endField = form && document.getElementById('end');
    var serverText = null;

    // The fields are Warsaw time, whatever the phone's own time zone is.
    var warsaw = form && new Intl.DateTimeFormat('en-GB', {
        timeZone: 'Europe/Warsaw', hourCycle: 'h23',
        year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit'
    });

    // Warsaw wall-clock time (to the minute) of a moment, as if it were UTC.
    function toWarsaw(utc) {
        var parts = {};
        warsaw.formatToParts(utc).forEach(function (part) {
            parts[part.type] = Number(part.value);
        });
        return Date.UTC(parts.year, parts.month - 1, parts.day, parts.hour, parts.minute);
    }

    // Same rule as PolandTime.TryToUtc on the server: a time that happens twice is the later one (winter time, UTC+1),
    // a time that is skipped is null.
    function toUtc(wall) {
        return [wall - HOUR_MS, wall - 2 * HOUR_MS].filter(function (utc) {
            return toWarsaw(utc) === wall;
        })[0] || null;
    }

    // What is in a field, dd-mm-yyyy and HH:mm: empty, or its moment in UTC; neither when it is not a time.
    function read(field) {
        var dateText = field.querySelector('[data-time-field-date]').value.trim();
        var timeText = field.querySelector('[data-time-field-time]').value.trim();
        var d = /^(\d{2})-(\d{2})-(\d{4})$/.exec(dateText);
        var t = /^(\d{2}):(\d{2})$/.exec(timeText);
        var day = d && new Date(Date.UTC(+d[3], d[2] - 1, +d[1]));
        var valid = d && t && day.getUTCDate() === +d[1] && +t[1] < 24 && +t[2] < 60;
        return {
            text: dateText + ' ' + timeText,
            empty: dateText === '' && timeText === '',
            utc: valid ? toUtc(day.getTime() + t[1] * HOUR_MS + t[2] * MINUTE_MS) : null
        };
    }

    // since: ticks from there; text: stands still.
    function picked() {
        var start = read(startField);
        var end = read(endField);
        var runningSince = clock.dataset.runningSince ? Number(clock.dataset.runningSince) : null;
        var server = runningSince === null ? { text: serverText } : { since: runningSince };

        if (start.utc === null || (end.utc === null && !end.empty)) {
            return server;
        }
        if (end.utc !== null) {
            return { text: durationText(end.utc - start.utc, form.dataset) };
        }

        // Running from the minute in the field, or a new sleep left at "now": as the server gave it. The fields know
        // only the minute, the server the exact tap.
        var unchanged = runningSince === null
            ? start.text === form.elements.DefaultStart.value
            : toWarsaw(start.utc) === toWarsaw(runningSince);
        if (unchanged) {
            return server;
        }

        // Not running: the time since the start, until Start or Resume.
        return runningSince === null ? { text: clockText(now() - start.utc) } : { since: start.utc };
    }

    function follow() {
        var shown = picked();
        ticking = [];
        if (shown.since === undefined) {
            clock.textContent = shown.text;
        } else {
            ticking.push({ element: clock, since: shown.since, format: 'clock' });
        }
        run();
    }

    // --- What the server gave -----------------------------------------------------------------

    function refresh() {
        clockOffset = Number(document.querySelector('[data-server-now]').dataset.serverNow) - Date.now();
        if (form) {
            serverText = clock.textContent;
            follow();
            return;
        }

        ticking = Array.prototype.map.call(document.querySelectorAll('[data-running-since]'), function (element) {
            return { element: element, since: Number(element.dataset.runningSince), format: element.dataset.format };
        });
        run();
    }

    if (form) {
        [startField, endField].forEach(function (field) {
            // She picked a time, or sleep-timer-api.js set the field.
            field.addEventListener('change', follow);
            field.addEventListener('time-field:refresh', follow);
        });
    }

    document.addEventListener('sleep-timer:refresh', refresh);
    refresh();
})();
