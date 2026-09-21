document.addEventListener('DOMContentLoaded', function () {
    var listenBtn = document.getElementById('listenBtn');
    if (listenBtn && 'speechSynthesis' in window) {
        listenBtn.addEventListener('click', function () {
            if (speechSynthesis.speaking) { speechSynthesis.cancel(); return; }
            var main = document.querySelector('main');
            speechSynthesis.speak(new SpeechSynthesisUtterance(main.innerText));
        });
    }

    document.querySelectorAll('.pin-btn').forEach(function (btn) {
        btn.addEventListener('click', function () {
            var icon = btn.querySelector('i');
            var pinned = icon.classList.toggle('bi-pin-angle-fill');
            icon.classList.toggle('bi-pin-angle', !pinned);
            btn.closest('.course-card-wrap').classList.toggle('cat-pinned', pinned);
        });
    });

    document.querySelectorAll('.course-filter').forEach(function (btn) {
        btn.addEventListener('click', function () {
            document.querySelectorAll('.course-filter').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            var filter = btn.dataset.filter;
            document.querySelectorAll('.course-card-wrap').forEach(function (card) {
                card.style.display = (filter === 'all' || card.classList.contains('cat-' + filter)) ? '' : 'none';
            });
        });
    });
});