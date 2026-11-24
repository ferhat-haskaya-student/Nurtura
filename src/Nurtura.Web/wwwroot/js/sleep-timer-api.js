// Start, Stop and Resume on the timer screen (Areas/Mother/Views/Sleep/Timer.cshtml) without a page reload, through the
// API (/api/sleep). Every other button, and any browser without this script, posts the form as usual.
// The screen shows what the server answers from the database: after a success exactly the page she would get by
// reloading, after a refusal the state as it is, with what she entered and the message.
// Texts go on the page with textContent and value, never innerHTML.
(function () {
    var form = document.querySelector('[data-sleep-form]');
    if (!form || !window.fetch || !window.URLSearchParams) {
        return;
    }

    var COMMANDS = ['start', 'stop', 'resume'];
    var page = document.querySelector('[data-server-now]');
    var longRunning = document.querySelector('[data-sleep-long-running]');
    var messages = form.querySelector('[data-sleep-messages]');
    var clock = form.querySelector('[data-sleep-clock]');
    var reset = document.querySelector('[data-sleep-reset]');
    var resetForm = document.querySelector('[data-sleep-reset-form]');
    var details = document.getElementById('sleep-details');
    var detailsButton = form.querySelector('[data-bs-target="#sleep-details"]');
    var note = form.elements.Note;
    var fieldMessages = form.querySelectorAll('[data-valmsg-for]');
    var busy = false;

    // Read out when a message appears, like the ones on top of the form.
    fieldMessages.forEach(function (element) {
        element.setAttribute('aria-live', 'polite');
    });

    function bigButton() {
        return form.querySelector('[data-sleep-button]');
    }

    // --- Messages -----------------------------------------------------------------------------

    function clearMessages() {
        messages.textContent = '';
        fieldMessages.forEach(function (element) {
            element.textContent = '';
            element.classList.remove('field-validation-error');
            element.classList.add('field-validation-valid');
        });
        note.classList.remove('input-validation-error');
    }

    // On top of the form, where the server's own summary is.
    function showSummary(texts) {
        var box = document.createElement('div');
        var list = document.createElement('ul');
        box.className = 'alert alert-danger validation-summary-errors';
        texts.forEach(function (text) {
            var item = document.createElement('li');
            item.textContent = text;
            list.appendChild(item);
        });
        box.appendChild(list);
        messages.appendChild(box);
    }

    // errors: field name -> messages, "" for the summary. A field shows its first message, as on the server.
    function showErrors(errors) {
        Object.keys(errors).forEach(function (field) {
            var element = field === '' ? null : form.querySelector('[data-valmsg-for="' + field + '"]');
            if (!element) {
                showSummary(errors[field]);
                return;
            }

            element.textContent = errors[field][0];
            element.classList.remove('field-validation-valid');
            element.classList.add('field-validation-error');
            if (field === note.name) {
                note.classList.add('input-validation-error');
            }
        });
    }

    // --- The state ----------------------------------------------------------------------------

    function showClock(screen) {
        var running = screen.runningSince !== null;
        clock.classList.toggle('text-body-secondary', !running && screen.durationText === null);
        if (running) {
            clock.setAttribute('aria-live', 'off');
            clock.dataset.runningSince = screen.runningSince;
            clock.dataset.format = 'clock';
        } else {
            clock.removeAttribute('aria-live');
            delete clock.dataset.runningSince;
            delete clock.dataset.format;
            clock.textContent = screen.durationText === null ? '0:00:00' : screen.durationText;
        }

        page.dataset.serverNow = screen.serverNow;
        document.dispatchEvent(new Event('sleep-timer:refresh'));
    }

    function showButton(screen) {
        var button = bigButton();
        if (screen.command === null) {
            button.remove();
        } else {
            button.value = screen.command;
            button.textContent = screen.commandText;
        }
    }

    function showState(screen) {
        showClock(screen);
        showButton(screen);
        longRunning.classList.toggle('d-none', !screen.isLongRunning);
        form.elements.sleepId.value = screen.sleepId === null ? '' : screen.sleepId;
        reset.classList.toggle('d-none', screen.sleepId === null);
        if (screen.resetUrl !== null) {
            resetForm.setAttribute('action', screen.resetUrl);
        }
    }

    // --- Her fields ---------------------------------------------------------------------------

    // What she changed while the answer was on its way stays.
    function setFields(screen, sent) {
        function unchanged(name) {
            return form.elements[name].value === sent.get(name);
        }

        function set(name, value) {
            if (unchanged(name)) {
                form.elements[name].value = value === null ? '' : value;
            }
        }

        function setTime(id, dateName, timeName, date, time) {
            if (unchanged(dateName) && unchanged(timeName)) {
                set(dateName, date);
                set(timeName, time);
                document.getElementById(id).dispatchEvent(new Event('time-field:refresh'));
            }
        }

        setTime('start', 'StartDate', 'StartTime', screen.startDate, screen.startTime);
        setTime('end', 'EndDate', 'EndTime', screen.endDate, screen.endTime);
        form.elements.DefaultStart.value = '';
        set('IsShared', String(screen.isShared));
        set('Note', screen.note);
        // note-counter.js listens for input events.
        note.dispatchEvent(new Event('input'));
    }

    // Folded unless there is something in them to see, as when the page is made.
    function showDetails(hasNoteMessage) {
        var open = note.value !== '' || form.elements.IsShared.value === 'true' || hasNoteMessage;
        if (window.bootstrap) {
            var collapse = bootstrap.Collapse.getOrCreateInstance(details, { toggle: false });
            if (open) {
                collapse.show();
            } else {
                collapse.hide();
            }
        } else {
            details.classList.toggle('show', open);
            detailsButton.classList.toggle('collapsed', !open);
            detailsButton.setAttribute('aria-expanded', String(open));
        }
    }

    // Reload, Back and the language switch lead to this sleep.
    function setAddress(url) {
        form.dataset.pageUrl = url;
        if (location.pathname !== url) {
            history.replaceState(history.state, '', url);
        }
        document.querySelectorAll('form input[name="returnUrl"]').forEach(function (input) {
            input.value = url;
        });
    }

    // --- The call -----------------------------------------------------------------------------

    function setDisabled(disabled) {
        form.querySelectorAll('button[type="submit"]').forEach(function (button) {
            button.disabled = disabled;
        });
    }

    // Answered: tappable again. Disabling took the keyboard focus away from the big button; give it back.
    function ready() {
        var button = bigButton();
        busy = false;
        setDisabled(false);
        if (button && (document.activeElement === null || document.activeElement === document.body)) {
            button.focus({ preventScroll: true });
        }
    }

    function saved(screen, sent) {
        clearMessages();
        showState(screen);
        setFields(screen, sent);
        showDetails(false);
        setAddress(screen.editUrl);
        ready();
    }

    function refused(problem) {
        clearMessages();
        showState(problem.screen);
        showErrors(problem.errors);
        showDetails(note.name in problem.errors);
        ready();
    }

    function failed() {
        clearMessages();
        showSummary([form.dataset.textSaveFailed]);
        ready();
    }

    // Not signed in any more, onboarding, a page not found: the normal page takes over.
    // The screen's own address, so a page shown after a refused Save is not sent again.
    function reload() {
        location.replace(form.dataset.pageUrl);
    }

    function isJson(response) {
        return /^application\/(problem\+)?json\b/.test(response.headers.get('Content-Type') || '');
    }

    function send(command) {
        var sent = new FormData(form);
        busy = true;
        setDisabled(true);
        fetch(form.dataset.apiUrl + command, {
            method: 'POST',
            headers: { 'RequestVerificationToken': form.elements.__RequestVerificationToken.value },
            body: new URLSearchParams(sent),
            credentials: 'same-origin',
            redirect: 'manual'
        }).then(function (response) {
            if ((response.status !== 200 && response.status !== 422) || !isJson(response)) {
                return reload;
            }

            return response.json().then(function (json) {
                return function () {
                    if (response.status === 200) {
                        saved(json, sent);
                    } else {
                        refused(json);
                    }
                };
            });
        }).catch(function (error) {
            // A SyntaxError is an answer that is not JSON; anything else is the network.
            return error instanceof SyntaxError ? reload : failed;
        }).then(function (show) {
            show();
        });
    }

    form.addEventListener('submit', function (event) {
        var submitter = event.submitter;
        if (!submitter || submitter.name !== 'command' || COMMANDS.indexOf(submitter.value) === -1) {
            return;
        }

        // Stopped by client validation: its own handler shows why.
        var jquery = window.jQuery;
        if (event.defaultPrevented || (jquery && jquery.fn.valid && jquery(form).data('validator') && !jquery(form).valid())) {
            return;
        }

        event.preventDefault();
        if (!busy) {
            send(submitter.value);
        }
    });
})();
