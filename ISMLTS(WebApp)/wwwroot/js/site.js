(() => {
    const bs = globalThis.bootstrap;

    // ---------- Toasts: TempData["Toast"] is rendered by the layout and shown once ----------
    for (const element of document.querySelectorAll('[data-app-toast]')) {
        bs.Toast.getOrCreateInstance(element, { delay: 5000 }).show();
    }

    // ---------- Tooltips on icon buttons ----------
    for (const element of document.querySelectorAll('[data-bs-toggle="tooltip"]')) {
        bs.Tooltip.getOrCreateInstance(element);
    }

    // ---------- data-confirm: destructive forms go through one shared modal ----------
    const confirmElement = document.getElementById('confirmModal');
    if (confirmElement) {
        const confirmModal = bs.Modal.getOrCreateInstance(confirmElement);
        const message = confirmElement.querySelector('[data-confirm-message]');
        const okButton = confirmElement.querySelector('[data-confirm-ok]');
        let pendingForm = null;
        let pendingSubmitter = null;

        // Capture phase, so this runs before any other submit handler on the form.
        // data-confirm can sit on the form, or on one submit button when a form has several.
        document.addEventListener('submit', (event) => {
            const form = event.target;
            const source = event.submitter?.dataset.confirm ? event.submitter : form;
            if (!(form instanceof HTMLFormElement) || !source.dataset.confirm || form.dataset.confirmed === 'true') {
                return;
            }
            event.preventDefault();
            event.stopPropagation();
            pendingForm = form;
            pendingSubmitter = event.submitter ?? null;
            message.textContent = source.dataset.confirm;
            okButton.textContent = source.dataset.confirmLabel || 'Delete';
            confirmModal.show();
        }, true);

        okButton.addEventListener('click', () => {
            if (!pendingForm) {
                return;
            }
            pendingForm.dataset.confirmed = 'true';
            confirmModal.hide();
            pendingForm.requestSubmit(pendingSubmitter);
        });

        confirmElement.addEventListener('hidden.bs.modal', () => {
            if (pendingForm && pendingForm.dataset.confirmed !== 'true') {
                pendingForm = null;
            }
        });
    }

    // ---------- data-loading: disable the submit button and show a spinner (no double posts) ----------
    const startLoading = (button) => {
        button.dataset.originalLabel = button.innerHTML;
        const spinner = document.createElement('span');
        spinner.className = 'spinner-border spinner-border-sm me-1';
        spinner.setAttribute('aria-hidden', 'true');
        const label = button.dataset.loading || button.textContent.trim();
        button.replaceChildren(spinner, document.createTextNode(label));
        button.disabled = true;
    };

    document.addEventListener('submit', (event) => {
        if (event.defaultPrevented) {
            return;
        }
        const form = event.target;
        const button = event.submitter?.hasAttribute('data-loading')
            ? event.submitter
            : form.querySelector('[type="submit"][data-loading]');
        if (button) {
            // Next tick, so the browser has already collected the button's name/value
            setTimeout(() => startLoading(button), 0);
        }
    });

    // Coming back with the Back button restores the page from cache; re-enable its buttons
    globalThis.addEventListener('pageshow', (event) => {
        if (!event.persisted) {
            return;
        }
        for (const button of document.querySelectorAll('[data-loading][data-original-label]')) {
            button.innerHTML = button.dataset.originalLabel;
            button.disabled = false;
            delete button.dataset.originalLabel;
        }
    });

    // ---------- data-table-filter: instant search over a table's rows or [data-filter-item]s ----------
    for (const input of document.querySelectorAll('[data-table-filter]')) {
        const target = document.querySelector(input.dataset.tableFilter);
        if (!target) {
            continue;
        }
        const rows = [...target.querySelectorAll('tbody tr, [data-filter-item]')];
        const noMatches = (input.closest('.panel') ?? document).querySelector('[data-filter-empty]');
        input.addEventListener('input', () => {
            const term = input.value.trim().toLowerCase();
            let shown = 0;
            for (const row of rows) {
                const match = row.textContent.toLowerCase().includes(term);
                row.hidden = !match;
                if (match) {
                    shown++;
                }
            }
            if (noMatches) {
                noMatches.hidden = shown > 0;
            }
        });
    }

    // ---------- Notification bell: opening it marks the notifications shown as read ----------
    for (const bell of document.querySelectorAll('[data-notification-bell]')) {
        const form = bell.parentElement.querySelector('form[data-mark-seen]');
        if (!form) {
            continue;
        }
        bell.addEventListener('shown.bs.dropdown', () => {
            if (form.dataset.sent === 'true') {
                return;
            }
            form.dataset.sent = 'true';
            fetch(form.action, { method: 'POST', body: new FormData(form) })
                .then((response) => (response.ok ? response.json() : Promise.reject(new Error(String(response.status)))))
                .then((result) => {
                    const badge = bell.querySelector('[data-notification-count]');
                    if (result.unread > 0 && badge) {
                        badge.textContent = result.unread > 9 ? '9+' : String(result.unread);
                    } else {
                        badge?.remove();
                    }
                    bell.setAttribute('aria-label', result.unread > 0 ? `Notifications, ${result.unread} unread` : 'Notifications');
                })
                .catch(() => {
                    form.dataset.sent = 'false';
                });
        });
    }

    // ---------- data-check-all: one checkbox ticks every visible checkbox in a list ----------
    for (const master of document.querySelectorAll('[data-check-all]')) {
        const list = document.querySelector(master.dataset.checkAll);
        if (!list) {
            continue;
        }
        master.addEventListener('change', () => {
            for (const box of list.querySelectorAll('input[type="checkbox"]')) {
                if (!box.closest('[hidden]')) {
                    box.checked = master.checked;
                }
            }
        });
    }

    // ---------- Password fields: show/hide toggle and a Caps Lock hint ----------
    for (const button of document.querySelectorAll('[data-password-toggle]')) {
        const input = document.getElementById(button.dataset.passwordToggle);
        const icon = button.querySelector('i');
        button.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(show));
            button.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
            icon.classList.toggle('bi-eye', !show);
            icon.classList.toggle('bi-eye-slash', show);
        });
    }

    for (const input of document.querySelectorAll('[data-capslock-hint]')) {
        const hint = document.getElementById(input.dataset.capslockHint);
        const update = (event) => {
            hint.hidden = !event.getModifierState?.('CapsLock');
        };
        input.addEventListener('keydown', update);
        input.addEventListener('keyup', update);
        input.addEventListener('blur', () => {
            hint.hidden = true;
        });
    }

    // ---------- Student dashboard ----------
    const listenButton = document.getElementById('listenBtn');
    if (listenButton && 'speechSynthesis' in globalThis) {
        listenButton.addEventListener('click', () => {
            if (speechSynthesis.speaking) {
                speechSynthesis.cancel();
                return;
            }
            speechSynthesis.speak(new SpeechSynthesisUtterance(document.querySelector('main').innerText));
        });
    }

    for (const button of document.querySelectorAll('.pin-btn')) {
        button.addEventListener('click', () => {
            const icon = button.querySelector('i');
            const pinned = icon.classList.toggle('bi-pin-angle-fill');
            icon.classList.toggle('bi-pin-angle', !pinned);
            button.setAttribute('aria-pressed', String(pinned));
            button.closest('.course-card-wrap').classList.toggle('cat-pinned', pinned);
        });
    }

    const courseFilters = document.querySelectorAll('.course-filter');
    for (const button of courseFilters) {
        button.addEventListener('click', () => {
            for (const other of courseFilters) {
                other.classList.toggle('active', other === button);
            }
            const filter = button.dataset.filter;
            for (const card of document.querySelectorAll('.course-card-wrap')) {
                card.hidden = !(filter === 'all' || card.classList.contains(`cat-${filter}`));
            }
        });
    }
})();
