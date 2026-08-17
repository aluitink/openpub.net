FB.register('menu', function() {
    'use strict';
    var menu = document.getElementById('nav-menu');
    var hamburger = document.getElementById('nav-hamburger');
    if (!menu || !hamburger) return;

    var body = document.body;
    var scrim = document.getElementById('nav-scrim');

    function openDrawer() {
        menu.classList.add('open');
        body.classList.add('nav-open');
        if (scrim) scrim.classList.add('visible');
        hamburger.setAttribute('aria-expanded', 'true');
        hamburger.setAttribute('aria-label', 'Close menu');
        var first = menu.querySelector('a[href], button');
        if (first) first.focus();
    }

    function closeDrawer() {
        menu.classList.remove('open');
        body.classList.remove('nav-open');
        if (scrim) scrim.classList.remove('visible');
        hamburger.setAttribute('aria-expanded', 'false');
        hamburger.setAttribute('aria-label', 'Open menu');
    }

    if (scrim) {
        scrim.addEventListener('click', closeDrawer);
    }

    hamburger.addEventListener('click', function() {
        if (menu.classList.contains('open')) closeDrawer();
        else openDrawer();
    });

    document.addEventListener('keydown', function(e) {
        if (e.key === 'Tab' && menu.classList.contains('open') && body.classList.contains('nav-open')) {
            var focusable = Array.prototype.slice.call(
                menu.querySelectorAll('a[href], button:not([disabled])')
            ).concat([hamburger]);
            if (!focusable.length) return;
            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        }
    });

    document.addEventListener('click', function(e) {
        if (!menu.classList.contains('open')) return;
        if (menu.contains(e.target) || hamburger.contains(e.target)) return;
        closeDrawer();
    });
});
