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
        const icon = confirmElement.querySelector('[data-confirm-icon]');
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
            const primary = source.dataset.confirmTone === 'primary';
            okButton.className = primary ? 'btn btn-rosebank' : 'btn btn-danger';
            icon?.classList.toggle('bi-exclamation-triangle', !primary);
            icon?.classList.toggle('text-danger', !primary);
            icon?.classList.toggle('bi-question-circle', primary);
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

    // ---------- data-table-filter + data-table-facet: instant search and "Show" dropdowns over a table's rows ----------
    // A facet select with data-facet="status" keeps rows whose data-status holds the chosen value (space-separated tokens).
    const rowMatches = (row, control) => {
        const value = control.value.trim().toLowerCase();
        if (!value) {
            return true;
        }
        if (control.dataset.tableFacet) {
            return (row.dataset[control.dataset.facet] ?? '').toLowerCase().split(' ').includes(value);
        }
        return row.textContent.toLowerCase().includes(value);
    };

    const filterGroups = new Map();
    for (const control of document.querySelectorAll('[data-table-filter], [data-table-facet]')) {
        const selector = control.dataset.tableFilter ?? control.dataset.tableFacet;
        filterGroups.set(selector, [...(filterGroups.get(selector) ?? []), control]);
    }

    for (const [selector, controls] of filterGroups) {
        const target = document.querySelector(selector);
        if (!target) {
            continue;
        }
        const rows = [...target.querySelectorAll('tbody tr, [data-filter-item]')];
        const noMatches = (controls[0].closest('.panel') ?? document).querySelector('[data-filter-empty]');
        const apply = () => {
            let shown = 0;
            for (const row of rows) {
                const match = controls.every((control) => rowMatches(row, control));
                row.hidden = !match;
                if (match) {
                    shown++;
                }
            }
            if (noMatches) {
                noMatches.hidden = shown > 0;
            }
        };
        for (const control of controls) {
            control.addEventListener(control.tagName === 'SELECT' ? 'change' : 'input', apply);
        }
        // Browsers refill the controls when coming back to the page
        if (controls.some((control) => control.value)) {
            apply();
        }
    }

    // ---------- table[data-sortable]: th[data-sort="text|number"] headers sort the rows on click ----------
    // A cell's data-sort-value wins over its text; empty values always go to the bottom.
    const collator = new Intl.Collator(undefined, { numeric: true, sensitivity: 'base' });
    const sortKey = (row, index, type) => {
        const cell = row.cells[index];
        const raw = (cell?.dataset.sortValue ?? cell?.textContent ?? '').trim();
        if (type === 'number') {
            const number = Number.parseFloat(raw);
            return Number.isNaN(number) ? null : number;
        }
        return raw === '' ? null : raw;
    };

    for (const table of document.querySelectorAll('table[data-sortable]')) {
        const body = table.tBodies[0];
        const headers = [...table.querySelectorAll('thead th[data-sort]')];
        const showState = () => {
            for (const header of headers) {
                const state = header.getAttribute('aria-sort');
                const icon = header.querySelector('.sort-icon');
                icon.classList.toggle('bi-chevron-expand', !state);
                icon.classList.toggle('bi-caret-up-fill', state === 'ascending');
                icon.classList.toggle('bi-caret-down-fill', state === 'descending');
            }
        };

        for (const header of headers) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'sort-button';
            button.append(...header.childNodes);
            const icon = document.createElement('i');
            icon.className = 'bi sort-icon';
            icon.setAttribute('aria-hidden', 'true');
            button.append(icon);
            header.append(button);

            button.addEventListener('click', () => {
                const ascending = header.getAttribute('aria-sort') !== 'ascending';
                for (const other of headers) {
                    other.removeAttribute('aria-sort');
                }
                header.setAttribute('aria-sort', ascending ? 'ascending' : 'descending');
                const keyed = [...body.rows].map((row) => ({ row, key: sortKey(row, header.cellIndex, header.dataset.sort) }));
                keyed.sort((a, b) => {
                    if (a.key === null || b.key === null) {
                        return Number(a.key === null) - Number(b.key === null);
                    }
                    const order = typeof a.key === 'number' ? a.key - b.key : collator.compare(a.key, b.key);
                    return ascending ? order : -order;
                });
                body.append(...keyed.map((item) => item.row));
                showState();
            });
        }
        showState();
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
