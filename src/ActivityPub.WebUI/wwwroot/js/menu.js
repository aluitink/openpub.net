(function () {
    'use strict';

    function closeAllGroups(except) {
        document.querySelectorAll('.nav-group.open').forEach(function (g) {
            if (g !== except) {
                g.classList.remove('open');
                var btn = g.querySelector('.nav-group-toggle');
                if (btn) btn.setAttribute('aria-expanded', 'false');
            }
        });
    }

    function focusables(container) {
        return Array.prototype.slice.call(
            container.querySelectorAll('a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])')
        ).filter(function (el) {
            return el.offsetParent !== null || el === document.activeElement;
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        var menu = document.getElementById('nav-menu');
        var hamburger = document.getElementById('nav-hamburger');
        if (!menu) return;

        menu.querySelectorAll('.nav-group-toggle').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                var group = btn.closest('.nav-group');
                if (!group) return;

                var willOpen = !group.classList.contains('open');
                closeAllGroups(group);
                group.classList.toggle('open', willOpen);
                btn.setAttribute('aria-expanded', willOpen ? 'true' : 'false');

                if (willOpen && window.matchMedia('(min-width: 769px)').matches) {
                    var first = group.querySelector('.nav-dropdown-link');
                    if (first) first.focus();
                }
            });

            btn.addEventListener('keydown', function (e) {
                if (e.key !== 'ArrowDown') return;
                e.preventDefault();
                var group = btn.closest('.nav-group');
                if (!group || !group.classList.contains('open')) {
                    btn.click();
                    return;
                }
                var first = group.querySelector('.nav-dropdown-link');
                if (first) first.focus();
            });
        });

        menu.querySelectorAll('.nav-dropdown').forEach(function (dropdown) {
            dropdown.addEventListener('keydown', function (e) {
                if (e.key !== 'Tab') return;
                var items = focusables(dropdown);
                if (!items.length) return;
                var first = items[0];
                var last = items[items.length - 1];

                if (e.shiftKey && document.activeElement === first) {
                    e.preventDefault();
                    last.focus();
                } else if (!e.shiftKey && document.activeElement === last) {
                    e.preventDefault();
                    first.focus();
                }
            });
        });

        if (hamburger) {
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

            hamburger.addEventListener('click', function () {
                if (menu.classList.contains('open')) closeDrawer();
                else openDrawer();
            });

            document.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') {
                    if (menu.classList.contains('open')) {
                        closeDrawer();
                        hamburger.focus();
                    }
                    closeAllGroups();
                }

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

            document.addEventListener('click', function (e) {
                if (!menu.classList.contains('open')) return;
                if (menu.contains(e.target) || hamburger.contains(e.target)) return;
                closeDrawer();
            });
        }

        document.addEventListener('click', function (e) {
            if (!e.target.closest('.nav-group')) {
                closeAllGroups();
            }
        });
    });
})();
