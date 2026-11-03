// The wheel time picker (Areas/Mother/Views/Shared/_TimePickerSheet.cshtml and _TimePickerField.cshtml).
// Each field keeps posting its plain dd-mm-yyyy and HH:mm inputs; this script hides them, shows a button
// ("Today, 13:06") and lets her pick with three scroll wheels: day, hour (00-23), minute (00-59).
// Wheels are plain CSS scroll-snap lists: touch, mouse wheel, click and arrow keys all work.
// Dates are handled as Warsaw wall-clock values in UTC Date objects, so the phone's own time zone never shifts them.
(function () {
    var root = document.querySelector('[data-time-picker]');
    var fields = document.querySelectorAll('[data-time-field]');
    if (!root || fields.length === 0 || typeof bootstrap === 'undefined') {
        return;
    }

    var ITEM_HEIGHT = 40;
    var DAY_MS = 86400000;
    var data = root.dataset;
    var weekdays = JSON.parse(data.weekdays);
    var loadedAt = Date.now();
    var nowAtLoad = parseIso(data.now);
    var daysBack = Number(data.daysBack);

    var body = root.querySelector('[data-time-picker-body]');
    var offcanvasElement = root.querySelector('[data-time-picker-offcanvas]');
    var modalElement = root.querySelector('[data-time-picker-modal]');
    var offcanvas = new bootstrap.Offcanvas(offcanvasElement);
    var modal = new bootstrap.Modal(modalElement);
    var wide = window.matchMedia('(min-width: 768px)');
    var shell = null;
    var field = null;

    var wheels = {
        day: root.querySelector('[data-wheel="day"]'),
        hour: root.querySelector('[data-wheel="hour"]'),
        minute: root.querySelector('[data-wheel="minute"]')
    };

    function pad(n) {
        return n < 10 ? '0' + n : String(n);
    }

    function format(template) {
        var args = Array.prototype.slice.call(arguments, 1);
        return template.replace(/\{(\d)\}/g, function (match, i) { return args[i]; });
    }

    // "2026-11-03T13:06" (Warsaw) -> Date whose UTC fields are that wall-clock time.
    function parseIso(value) {
        var m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
        return new Date(Date.UTC(+m[1], m[2] - 1, +m[3], +m[4], +m[5]));
    }

    // Warsaw now: the server's time when the page was made, plus the time since.
    function now() {
        var date = new Date(nowAtLoad.getTime() + (Date.now() - loadedAt));
        date.setUTCSeconds(0, 0);
        return date;
    }

    function dayOnly(date) {
        return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
    }

    function dateText(day) {
        var date = new Date(day);
        return pad(date.getUTCDate()) + '-' + pad(date.getUTCMonth() + 1) + '-' + date.getUTCFullYear();
    }

    function dayLabel(day) {
        var today = dayOnly(now());
        if (day === today) {
            return data.labelToday;
        }
        if (day === today - DAY_MS) {
            return data.labelYesterday;
        }
        var date = new Date(day);
        return format(data.labelDay, weekdays[date.getUTCDay()], pad(date.getUTCDate()) + '-' + pad(date.getUTCMonth() + 1));
    }

    // The field's current value, or null when it is empty or not a valid dd-mm-yyyy / HH:mm.
    function readField(target) {
        var d = /^(\d{2})-(\d{2})-(\d{4})$/.exec(target.date.value.trim());
        var t = /^(\d{2}):(\d{2})$/.exec(target.time.value.trim());
        if (!d || !t || +t[1] > 23 || +t[2] > 59) {
            return null;
        }
        return new Date(Date.UTC(+d[3], d[2] - 1, +d[1], +t[1], +t[2]));
    }

    function showLabel(target, value) {
        target.button.textContent = value
            ? format(data.labelDisplay, dayLabel(dayOnly(value)), pad(value.getUTCHours()) + ':' + pad(value.getUTCMinutes()))
            : data.labelNotSet;
    }

    // --- Wheels -------------------------------------------------------------------------------

    function fill(wheel, items) {
        wheel.innerHTML = '';
        items.forEach(function (item, index) {
            var option = document.createElement('div');
            option.className = 'time-wheel-item';
            option.id = wheel.dataset.wheel + '-option-' + index;
            option.setAttribute('role', 'option');
            option.setAttribute('aria-selected', 'false');
            option.dataset.value = item.value;
            option.textContent = item.label;
            option.addEventListener('click', function () {
                select(wheel, index, true);
            });
            wheel.appendChild(option);
        });
        wheel.dataset.index = '0';
    }

    function select(wheel, index, smooth) {
        var count = wheel.children.length;
        index = Math.max(0, Math.min(count - 1, index));
        mark(wheel, index);
        wheel.scrollTo({ top: index * ITEM_HEIGHT, behavior: smooth ? 'smooth' : 'auto' });
    }

    function mark(wheel, index) {
        var previous = wheel.children[Number(wheel.dataset.index)];
        if (previous) {
            previous.setAttribute('aria-selected', 'false');
        }
        wheel.dataset.index = String(index);
        var option = wheel.children[index];
        option.setAttribute('aria-selected', 'true');
        wheel.setAttribute('aria-activedescendant', option.id);
    }

    function value(wheel) {
        return Number(wheel.children[Number(wheel.dataset.index)].dataset.value);
    }

    function indexOf(wheel, wanted) {
        for (var i = 0; i < wheel.children.length; i++) {
            if (Number(wheel.children[i].dataset.value) === wanted) {
                return i;
            }
        }
        return 0;
    }

    Object.keys(wheels).forEach(function (name) {
        var wheel = wheels[name];
        var settle = null;

        // After scrolling stops, the option in the middle band is the chosen one.
        wheel.addEventListener('scroll', function () {
            clearTimeout(settle);
            settle = setTimeout(function () {
                var index = Math.round(wheel.scrollTop / ITEM_HEIGHT);
                if (index !== Number(wheel.dataset.index) && wheel.children[index]) {
                    mark(wheel, index);
                }
            }, 80);
        });

        wheel.addEventListener('keydown', function (event) {
            var index = Number(wheel.dataset.index);
            var moves = { ArrowUp: -1, ArrowDown: 1, PageUp: -5, PageDown: 5 };
            if (event.key in moves) {
                select(wheel, index + moves[event.key], true);
            } else if (event.key === 'Home') {
                select(wheel, 0, true);
            } else if (event.key === 'End') {
                select(wheel, wheel.children.length - 1, true);
            } else if (event.key === 'Enter') {
                save();
            } else {
                return;
            }
            event.preventDefault();
        });
    });

    function range(count, label) {
        var items = [];
        for (var i = 0; i < count; i++) {
            items.push({ value: i, label: label ? label(i) : pad(i) });
        }
        return items;
    }

    fill(wheels.hour, range(24));
    fill(wheels.minute, range(60));

    // Today back daysBack days, newest first. A field's own older (or, after midnight, newer) day is added too.
    function fillDays(extraDay) {
        var today = dayOnly(now());
        var days = [];
        for (var i = 0; i <= daysBack; i++) {
            days.push(today - i * DAY_MS);
        }
        if (extraDay !== null && days.indexOf(extraDay) === -1) {
            days.push(extraDay);
            days.sort(function (a, b) { return b - a; });
        }
        fill(wheels.day, days.map(function (day) { return { value: day, label: dayLabel(day) }; }));
    }

    function show(date) {
        select(wheels.day, indexOf(wheels.day, dayOnly(date)), false);
        select(wheels.hour, date.getUTCHours(), false);
        select(wheels.minute, date.getUTCMinutes(), false);
    }

    // --- Sheet --------------------------------------------------------------------------------

    function open(target) {
        field = target;
        var current = readField(target) || now();
        fillDays(dayOnly(current));
        root.querySelector('[data-picker-clear]').classList.toggle('d-none', !target.optional);

        shell = wide.matches ? modal : offcanvas;
        var shellElement = wide.matches ? modalElement : offcanvasElement;
        shellElement.querySelector('[data-time-picker-slot]').appendChild(body);
        shellElement.querySelector('[data-time-picker-title]').textContent = target.label;
        shell.show();
        // Wheels have no height until the shell is visible; set them again once it is.
        show(current);
        shellElement.addEventListener(wide.matches ? 'shown.bs.modal' : 'shown.bs.offcanvas', function () {
            show(current);
            wheels.day.focus();
        }, { once: true });
    }

    function close() {
        shell.hide();
        field.button.focus();
    }

    function write(date) {
        field.date.value = date ? dateText(dayOnly(date)) : '';
        field.time.value = date ? pad(date.getUTCHours()) + ':' + pad(date.getUTCMinutes()) : '';
        showLabel(field, date);
        // Lets a page react, like the sleep screen's clock.
        field.element.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function save() {
        var day = value(wheels.day);
        write(new Date(day + value(wheels.hour) * 3600000 + value(wheels.minute) * 60000));
        close();
    }

    root.querySelector('[data-picker-save]').addEventListener('click', save);
    root.querySelector('[data-picker-cancel]').addEventListener('click', close);
    root.querySelector('[data-picker-now]').addEventListener('click', function () {
        var current = now();
        fillDays(dayOnly(current));
        show(current);
    });
    root.querySelector('[data-picker-clear]').addEventListener('click', function () {
        write(null);
        close();
    });

    // --- Fields -------------------------------------------------------------------------------

    fields.forEach(function (element) {
        var target = {
            element: element,
            label: element.dataset.label,
            optional: element.dataset.optional === 'true',
            button: element.querySelector('[data-time-field-button]'),
            date: element.querySelector('[data-time-field-date]'),
            time: element.querySelector('[data-time-field-time]')
        };
        element.querySelector('[data-time-field-inputs]').classList.add('d-none');
        target.button.classList.remove('d-none');
        showLabel(target, readField(target));
        target.button.addEventListener('click', function () {
            open(target);
        });
    });
})();
