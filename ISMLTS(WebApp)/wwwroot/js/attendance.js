(() => {
    // Forms marked data-geo-form capture the browser's GPS position before submitting.
    for (const form of document.querySelectorAll('form[data-geo-form]')) {
        form.addEventListener('submit', (event) => {
            if (form.dataset.geoDone === 'true' || !('geolocation' in navigator)) {
                return;
            }
            event.preventDefault();

            const button = form.querySelector('[type="submit"]');
            const status = form.querySelector('[data-geo-status]');
            if (button) {
                button.disabled = true;
            }
            if (status) {
                status.textContent = 'Getting your location…';
            }

            const submitForm = () => {
                form.dataset.geoDone = 'true';
                form.requestSubmit();
            };

            navigator.geolocation.getCurrentPosition((position) => {
                form.elements.latitude.value = position.coords.latitude;
                form.elements.longitude.value = position.coords.longitude;
                if (form.elements.accuracy) {
                    form.elements.accuracy.value = Math.round(position.coords.accuracy);
                }
                submitForm();
            }, submitForm, { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 });
        });
    }

    // Countdown on the live QR page
    const timer = document.querySelector('[data-seconds-left]');
    if (timer) {
        let secondsLeft = Number.parseInt(timer.dataset.secondsLeft, 10);
        const tick = () => {
            if (secondsLeft <= 0) {
                timer.textContent = '0:00';
                return;
            }
            const minutes = Math.floor(secondsLeft / 60);
            const seconds = String(secondsLeft % 60).padStart(2, '0');
            timer.textContent = `${minutes}:${seconds}`;
            secondsLeft -= 1;
            setTimeout(tick, 1000);
        };
        tick();
    }

    // Live list of who has scanned in
    const live = document.querySelector('[data-refresh-url]');
    if (live) {
        setInterval(() => {
            fetch(live.dataset.refreshUrl)
                .then((response) => (response.ok ? response.text() : null))
                .then((html) => {
                    if (html !== null) {
                        live.innerHTML = html;
                    }
                })
                .catch(() => {
                    // Network blip: the next tick tries again.
                });
        }, 10000);
    }
})();