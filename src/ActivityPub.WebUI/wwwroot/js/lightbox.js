FB.register('lightbox', function() {
    'use strict';

    var overlay = document.getElementById('lightbox-overlay');
    var dialog = document.querySelector('.lightbox-dialog');
    var img = document.getElementById('lightbox-img');
    var counter = document.getElementById('lightbox-counter');
    var closeBtn = document.querySelector('[data-lightbox-close]');
    var prevBtn = document.querySelector('[data-lightbox-prev]');
    var nextBtn = document.querySelector('[data-lightbox-next]');
    if (!overlay || !dialog || !img) return;

    var isOpen = false;
    var items = [];
    var index = 0;
    var lastFocused = null;

    function focusables() {
        return Array.prototype.slice.call(
            dialog.querySelectorAll('button:not([disabled]), [href], input:not([disabled]), [tabindex]:not([tabindex="-1"])')
        );
    }

    function render() {
        var it = items[index];
        img.src = it.url;
        img.alt = it.alt;
        if (counter) counter.textContent = (index + 1) + ' of ' + items.length;
        // Hide single-image nav controls so they are not focusable / visible.
        var multi = items.length > 1;
        if (prevBtn) { prevBtn.hidden = !multi; }
        if (nextBtn) { nextBtn.hidden = !multi; }
    }

    function show(i) {
        if (!items.length) return;
        index = (i + items.length) % items.length;
        render();
    }

    function openFromImgs(imgs, startIndex, triggerEl) {
        if (!imgs || !imgs.length) return;
        items = Array.prototype.map.call(imgs, function(el) {
            return { url: el.currentSrc || el.src, alt: el.getAttribute('alt') || 'Attached image' };
        });
        // Remember where to return focus. A mouse-click on an <img> does not
        // move document.activeElement, so fall back to the clicked image.
        lastFocused = (document.activeElement && document.activeElement !== document.body)
            ? document.activeElement
            : (triggerEl || document.activeElement);
        index = Math.max(0, Math.min(startIndex || 0, items.length - 1));
        isOpen = true;
        overlay.hidden = false;
        overlay.setAttribute('aria-hidden', 'false');
        dialog.setAttribute('aria-hidden', 'false');
        document.body.classList.add('lightbox-open');
        render();
        requestAnimationFrame(function() { (closeBtn || img).focus(); });
    }

    function open(group, startIndex) {
        // Programmatic open of the first image group found on the page.
        openFromImgs(Array.prototype.slice.call(document.querySelectorAll('img.note-image')), startIndex);
    }

    function close() {
        if (!isOpen) return;
        isOpen = false;
        overlay.hidden = true;
        overlay.setAttribute('aria-hidden', 'true');
        dialog.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('lightbox-open');
        img.removeAttribute('src');
        if (lastFocused && typeof lastFocused.focus === 'function') lastFocused.focus();
    }

    // Open on click (or Enter/Space) of any note image. The image set is the
    // group of sibling .note-image elements in the same card.
    document.addEventListener('click', function(e) {
        var t = e.target;
        if (!t || !t.classList || !t.classList.contains('note-image')) return;
        openFromImg(t);
    });

    // Images are focusable buttons (role="button"); Enter/Space activates them
    // for keyboard-only users.
    document.addEventListener('keydown', function(e) {
        if (!isOpen) return;
        if ((e.key === 'Enter' || e.key === ' ') && e.target && e.target.classList && e.target.classList.contains('note-image')) {
            e.preventDefault();
            openFromImg(e.target);
        }
    });

    function openFromImg(img) {
        var groupEl = img.closest('[data-lightbox-group]');
        if (!groupEl) return;
        var imgs = Array.prototype.slice.call(groupEl.querySelectorAll('img.note-image'));
        var idx = 0;
        for (var i = 0; i < imgs.length; i++) { if (imgs[i] === img) { idx = i; break; } }
        openFromImgs(imgs, idx, img);
    }

    if (closeBtn) closeBtn.addEventListener('click', close);
    if (prevBtn) prevBtn.addEventListener('click', function() { show(index - 1); });
    if (nextBtn) nextBtn.addEventListener('click', function() { show(index + 1); });

    // Click on the backdrop (not the dialog) closes.
    overlay.addEventListener('mousedown', function(e) {
        if (e.target === overlay) close();
    });

    // Keyboard: arrows / Home / End navigate, Escape closes, Tab is trapped.
    document.addEventListener('keydown', function(e) {
        if (!isOpen) return;
        if (e.key === 'Escape') {
            e.preventDefault();
            close();
        } else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
            e.preventDefault();
            show(index - 1);
        } else if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
            e.preventDefault();
            show(index + 1);
        } else if (e.key === 'Home') {
            e.preventDefault();
            show(0);
        } else if (e.key === 'End') {
            e.preventDefault();
            show(items.length - 1);
        } else if (e.key === 'Tab') {
            var f = focusables();
            if (!f.length) return;
            var first = f[0], last = f[f.length - 1], active = document.activeElement;
            if (e.shiftKey && active === first) { e.preventDefault(); last.focus(); }
            else if (!e.shiftKey && active === last) { e.preventDefault(); first.focus(); }
        }
    });

    window.FB.lightbox = { open: open, close: close, show: show, isOpen: function() { return isOpen; } };
});
