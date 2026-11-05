// The consultation chat (Views/Shared/_ChatRoom.cshtml) over SignalR (/hubs/chat).
// The server decides everything: who may join, whether the room is open, who may edit and when the session ends.
// This script only shows what the server says. Every message text and name goes on the page with textContent,
// never innerHTML, so a message can never become HTML or script; line breaks stay through CSS pre-wrap.
(function () {
    var root = document.getElementById('chat');
    if (!root || !window.signalR) {
        return;
    }

    var appointmentId = parseInt(root.dataset.appointmentId, 10);
    var me = root.dataset.me;
    var text = root.dataset;

    var presenceEl = document.getElementById('chat-presence');
    var statusEl = document.getElementById('chat-status');
    var statusMainEl = document.getElementById('chat-status-main');
    var readOnlyEl = document.getElementById('chat-status-read-only');
    var connectionEl = document.getElementById('chat-connection');
    var listEl = document.getElementById('chat-messages');
    var emptyEl = document.getElementById('chat-empty');
    var errorEl = document.getElementById('chat-error');
    var form = document.getElementById('chat-form');
    var box = document.getElementById('chat-text');
    var sendButton = document.getElementById('chat-send');
    var cancelEditButton = document.getElementById('chat-cancel-edit');
    var editingEl = document.getElementById('chat-editing');
    var endEl = document.getElementById('chat-end');
    var endYesButton = document.getElementById('chat-end-yes');

    // Professional page only: the dot on the Chat tab and the summary tab.
    var chatPane = document.getElementById('chat-pane');
    var chatTab = document.getElementById('chat-tab');
    var chatDot = document.getElementById('chat-dot');
    var summaryContent = document.getElementById('summary-content');
    var summaryEnded = document.getElementById('summary-ended');

    var room = null;           // the last state from JoinRoom
    var connected = false;
    var editingId = null;      // the message being edited, if any
    var roomTimers = [];
    var messageEls = {};       // message id -> element

    var connection = new signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl)
        .withAutomaticReconnect()
        .build();

    function show(el, visible) {
        el.classList.toggle('d-none', !visible);
    }

    function showError(message) {
        errorEl.textContent = message || '';
        show(errorEl, !!message);
    }

    function updateCounter() {
        // note-counter.js listens for input events.
        box.dispatchEvent(new Event('input'));
    }

    function isOpen() {
        return connected && room !== null && room.state === 'Open';
    }

    function scrollToEnd() {
        listEl.scrollTop = listEl.scrollHeight;
    }

    // One message: own on the right, the other side's on the left; time, "edited", and Edit on her own
    // messages while the server says it is still editable.
    function buildMessage(message) {
        var mine = message.sender === me;
        var row = document.createElement('div');
        row.className = 'd-flex mb-2 ' + (mine ? 'justify-content-end' : 'justify-content-start');
        row.dataset.messageId = message.id;

        var bubble = document.createElement('div');
        bubble.className = 'rounded px-2 py-1 ' + (mine ? 'bg-primary-subtle' : 'bg-body-secondary');
        bubble.style.maxWidth = '85%';

        var body = document.createElement('div');
        body.className = 'text-break';
        body.style.whiteSpace = 'pre-wrap';
        body.textContent = message.text;
        bubble.appendChild(body);

        var meta = document.createElement('div');
        meta.className = 'small text-body-secondary d-flex flex-wrap gap-2 align-items-center ' + (mine ? 'justify-content-end' : '');
        var time = document.createElement('span');
        time.textContent = message.time;
        meta.appendChild(time);
        if (message.edited) {
            var edited = document.createElement('span');
            edited.textContent = text.textEdited;
            meta.appendChild(edited);
        }

        if (mine && message.editableFor > 0 && isOpen()) {
            var edit = document.createElement('button');
            edit.type = 'button';
            edit.className = 'btn btn-link btn-sm p-0 align-baseline';
            edit.textContent = text.textEdit;
            edit.addEventListener('click', function () {
                startEdit(message.id, body.textContent);
            });
            meta.appendChild(edit);
            // The server refuses edits after 3 minutes anyway; this only takes the button away on time.
            setTimeout(function () {
                edit.remove();
                if (editingId === message.id) {
                    stopEdit();
                }
            }, message.editableFor * 1000);
        }

        bubble.appendChild(meta);
        row.appendChild(bubble);
        return row;
    }

    function addMessage(message) {
        var el = buildMessage(message);
        messageEls[message.id] = el;
        listEl.appendChild(el);
        show(emptyEl, false);
    }

    function replaceMessage(message) {
        var old = messageEls[message.id];
        if (!old) {
            addMessage(message);
            return;
        }

        var el = buildMessage(message);
        messageEls[message.id] = el;
        old.replaceWith(el);
    }

    function startEdit(id, current) {
        editingId = id;
        box.value = current;
        updateCounter();
        show(editingEl, true);
        show(cancelEditButton, true);
        sendButton.textContent = text.textSave;
        box.focus();
    }

    function stopEdit() {
        editingId = null;
        box.value = '';
        updateCounter();
        show(editingEl, false);
        show(cancelEditButton, false);
        sendButton.textContent = text.textSend;
    }

    // Shows the room as the server last described it.
    function applyState() {
        var open = isOpen();

        show(presenceEl, open);
        if (open) {
            presenceEl.textContent = room.otherHere ? text.textOtherHere : text.textOtherAway;
        }

        if (room.state === 'NotYetOpen') {
            statusMainEl.textContent = text.textOpensAt;
            show(readOnlyEl, false);
            show(statusEl, true);
        } else if (room.state === 'Closed') {
            statusMainEl.textContent = room.endedByName
                ? text.textEndedBy.replace('{0}', room.endedByName)
                : text.textEndedAt.replace('{0}', room.end);
            show(readOnlyEl, true);
            show(statusEl, true);
        } else {
            show(statusEl, false);
        }

        // Before the room opens the box is shown but nothing can be sent; after it closes it is gone.
        show(form, room.state !== 'Closed');
        box.disabled = !open;
        sendButton.disabled = !open;
        if (!open && editingId !== null) {
            stopEdit();
        }

        show(endEl, open && room.canEndSession);

        // The summary is shown only until the session ends.
        if (room.state === 'Closed' && summaryContent) {
            summaryContent.remove();
            summaryContent = null;
            show(summaryEnded, true);
        }
    }

    function render(state) {
        room = state;
        roomTimers.forEach(clearTimeout);
        roomTimers = [];

        listEl.querySelectorAll('[data-message-id]').forEach(function (el) {
            el.remove();
        });
        messageEls = {};
        state.messages.forEach(addMessage);
        show(emptyEl, state.messages.length === 0);
        applyState();
        scrollToEnd();

        // Ask the server again when the room opens, when the session starts (End session appears)
        // and when it closes. The seconds come from the server clock.
        [state.secondsToOpen, state.secondsToStart, state.secondsToClose].forEach(function (seconds) {
            if (seconds > 0) {
                roomTimers.push(setTimeout(refresh, seconds * 1000 + 500));
            }
        });
    }

    // Loads the room and all its messages; used on start, after a reconnect and on each timer.
    function refresh() {
        return connection.invoke('JoinRoom', appointmentId)
            .then(render)
            .catch(function () {
                showError(text.textLoadError);
            });
    }

    connection.on('MessageReceived', function (message) {
        if (messageEls[message.id]) {
            return;
        }

        addMessage(message);
        scrollToEnd();
        if (message.sender !== me && chatDot && chatPane && !chatPane.classList.contains('active')) {
            show(chatDot, true);
        }
    });

    connection.on('MessageEdited', function (message) {
        replaceMessage(message);
    });

    connection.on('PresenceChanged', function (party, here) {
        if (room && party !== me) {
            room.otherHere = here;
            applyState();
        }
    });

    connection.on('SessionEnded', function () {
        refresh();
    });

    connection.onreconnecting(function () {
        connected = false;
        connectionEl.textContent = text.textReconnecting;
        show(connectionEl, true);
        if (room) {
            applyState();
        }
    });

    connection.onreconnected(function () {
        connected = true;
        show(connectionEl, false);
        // A new connection: join again and reload every message, so nothing sent meanwhile is missed.
        refresh();
    });

    connection.onclose(function () {
        connected = false;
        connectionEl.textContent = text.textDisconnected;
        show(connectionEl, true);
        if (room) {
            applyState();
        }
    });

    form.addEventListener('submit', function (event) {
        event.preventDefault();
        var value = box.value.trim();
        if (!value || !isOpen()) {
            return;
        }

        showError('');
        sendButton.disabled = true;
        var call = editingId !== null
            ? connection.invoke('EditMessage', appointmentId, editingId, value)
            : connection.invoke('SendMessage', appointmentId, value);
        var wasEditing = editingId !== null;
        call
            .then(function () {
                stopEdit();
            })
            .catch(function () {
                showError(wasEditing ? text.textEditError : text.textSendError);
                refresh();
            })
            .finally(function () {
                sendButton.disabled = !isOpen();
            });
    });

    cancelEditButton.addEventListener('click', stopEdit);

    endYesButton.addEventListener('click', function () {
        endYesButton.disabled = true;
        showError('');
        connection.invoke('EndSession', appointmentId)
            .catch(function () {
                showError(text.textEndError);
                refresh();
            })
            .finally(function () {
                endYesButton.disabled = false;
            });
    });

    if (chatTab && chatDot) {
        chatTab.addEventListener('shown.bs.tab', function () {
            show(chatDot, false);
            scrollToEnd();
        });
    }

    connection.start()
        .then(function () {
            connected = true;
            return refresh();
        })
        .catch(function () {
            connectionEl.textContent = text.textDisconnected;
            show(connectionEl, true);
        });
})();
