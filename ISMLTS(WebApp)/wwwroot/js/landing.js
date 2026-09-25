(() => {
    const root = document.querySelector('.landing');
    if (!root || globalThis.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        return;
    }

    root.classList.add('lp-js');
    const layers = root.querySelectorAll('[data-speed]');
    let ticking = false;

    const update = () => {
        const vh = globalThis.innerHeight;
        for (const el of layers) {
            const box = el.closest('section').getBoundingClientRect();
            if (box.bottom > 0 && box.top < vh) {
                const offset = (box.top + box.height / 2 - vh / 2) * -Number.parseFloat(el.dataset.speed);
                el.style.transform = `translate3d(0, ${offset.toFixed(1)}px, 0)`;
            }
        }
        ticking = false;
    };

    globalThis.addEventListener('scroll', () => {
        if (!ticking) {
            ticking = true;
            requestAnimationFrame(update);
        }
    }, { passive: true });
    globalThis.addEventListener('resize', update);
    update();

    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                entry.target.classList.add('is-visible');
                observer.unobserve(entry.target);
            }
        }
    }, { threshold: 0.15 });

    for (const el of root.querySelectorAll('.lp-reveal')) {
        observer.observe(el);
    }
})();