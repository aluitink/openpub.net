(function() {
    'use strict';

    // Command palette (Ctrl+K / Cmd+K): a fuzzy global search overlay across
    // notes, people, hashtags, and communities. Results are fetched from
    // /search/json and fuzzy-scored client-side so the overlay stays instant.
    FB.register('palette', function() {
        var overlay = document.getElementById('palette-overlay');
        var dialog = overlay ? overlay.querySelector('.palette-dialog') : null;
        var input = document.getElementById('palette-input');
        var results = document.getElementById('palette-results');
        var trigger = document.getElementById('palette-trigger');
        if (!overlay || !dialog || !input || !results) return;

        var MIN_CHARS = 1;
        var DEBOUNCE_MS = 120;
        var MAX_PER_GROUP = 6;

        var items = [];          // flat list of currently rendered results
        var activeIndex = 0;
        var isOpen = false;
        var lastFocused = null;
        var debounceTimer = null;
        var inFlight = 0;
        var seq = 0;

        // ---- Fuzzy scoring --------------------------------------------------
        // Subsequence match with a position/recency bonus; returns 0 for no
        // match. The query must appear as an ordered subsequence of the text.
        function fuzzyScore(query, text) {
            if (!text) return 0;
            var q = query.toLowerCase();
            var t = text.toLowerCase();
            if (!q) return 0;

            // Fast path: exact substring is always a strong hit.
            var idx = t.indexOf(q);
            if (idx !== -1) return 1000 - idx;

            var score = 0;
            var ti = 0;
            var prevMatch = -2;
            for (var qi = 0; qi < q.length; qi++) {
                var ch = q.charAt(qi);
                var found = -1;
                for (var k = ti; k < t.length; k++) {
                    if (t.charAt(k) === ch) { found = k; break; }
                }
                if (found === -1) return 0;
                if (found === prevMatch + 1) score += 4;   // consecutive run bonus
                else score += 1;
                score -= Math.min(found, 40) * 0.1;         // earlier is better
                prevMatch = found;
                ti = found + 1;
            }
            // Reward matches that start at the beginning of the text.
            score += (t.indexOf(q.charAt(0)) === 0) ? 8 : 0;
            return score;
        }

        function escapeHtml(s) {
            return (s || '')
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;');
        }

        // Wrap the first substring occurrence of `query` in <mark>.
        function highlight(text, query) {
            var safe = escapeHtml(text);
            if (!query) return safe;
            var lower = text.toLowerCase();
            var q = query.toLowerCase();
            var i = lower.indexOf(q);
            if (i === -1) return safe;
            var before = escapeHtml(text.substring(0, i));
            var match = escapeHtml(text.substring(i, i + q.length));
            var after = escapeHtml(text.substring(i + q.length));
            return before + '<mark>' + match + '</mark>' + after;
        }

        // ---- Building the result model --------------------------------------
        function buildItems(data, query) {
            var out = [];
            if (!data) return out;

            (data.users || []).forEach(function(u) {
                var primary = (u.displayName || u.username || '');
                var score = Math.max(
                    fuzzyScore(query, primary),
                    fuzzyScore(query, u.username || '') * 0.9
                );
                if (score > 0) {
                    out.push({
                        group: 'People',
                        icon: '@',
                        title: primary,
                        sub: u.username ? '@' + u.username : '',
                        count: u.bio || '',
                        score: score,
                        href: '/Profile?username=' + encodeURIComponent(u.username),
                        action: 'navigate'
                    });
                }
            });

            (data.communities || []).forEach(function(c) {
                var score = Math.max(
                    fuzzyScore(query, c.name || ''),
                    fuzzyScore(query, c.summary || '') * 0.6
                );
                if (score > 0) {
                    out.push({
                        group: 'Communities',
                        icon: 'cmd',
                        title: c.name || '',
                        sub: c.summary || '',
                        count: '',
                        score: score,
                        href: '/communities/show?communityId=' + encodeURIComponent(c.id),
                        action: 'navigate'
                    });
                }
            });

            (data.hashtags || []).forEach(function(h) {
                var tag = h.tag || '';
                var score = fuzzyScore(query, tag);
                if (score > 0) {
                    out.push({
                        group: 'Hashtags',
                        icon: '#',
                        title: '#' + tag,
                        sub: 'tag',
                        count: h.count ? String(h.count) : '',
                        score: score,
                        href: '/search?q=' + encodeURIComponent(tag) + '&tab=hashtags',
                        action: 'navigate'
                    });
                }
            });

            (data.notes || []).forEach(function(n) {
                var score = Math.max(
                    fuzzyScore(query, n.content || ''),
                    fuzzyScore(query, n.authorName || '') * 0.5
                );
                if (score > 0) {
                    out.push({
                        group: 'Notes',
                        icon: 'quote',
                        title: n.content || '',
                        sub: n.authorName ? 'by ' + n.authorName : '',
                        count: '',
                        score: score,
                        href: n.activityId
                            ? '/timeline?note=' + encodeURIComponent(n.activityId)
                            : '/search?q=' + encodeURIComponent(query),
                        action: 'navigate'
                    });
                }
            });

            out.sort(function(a, b) { return b.score - a.score; });
            return out;
        }

        // ---- Rendering ------------------------------------------------------
        function render() {
            results.innerHTML = '';
            if (!items.length) {
                var empty = document.createElement('div');
                empty.className = 'palette-empty';
                empty.textContent = 'No matches. Press Enter to search the full feed.';
                results.appendChild(empty);
                activeIndex = -1;
                return;
            }

            var groupOrder = ['People', 'Communities', 'Hashtags', 'Notes'];
            var flat = [];

            groupOrder.forEach(function(group) {
                var groupItems = items
                    .filter(function(it) { return it.group === group; })
                    .slice(0, MAX_PER_GROUP);
                if (!groupItems.length) return;

                var section = document.createElement('div');
                section.className = 'palette-group';

                var label = document.createElement('div');
                label.className = 'palette-group-label';
                label.textContent = group;
                section.appendChild(label);

                groupItems.forEach(function(it) {
                    var btn = document.createElement('button');
                    btn.type = 'button';
                    btn.className = 'palette-item';
                    btn.setAttribute('role', 'option');
                    btn.setAttribute('aria-selected', 'false');

                    var icon = document.createElement('span');
                    icon.className = 'palette-item-icon';
                    icon.setAttribute('aria-hidden', 'true');
                    // Icon names render as the shared SVG set; plain text
                    // chars (e.g. "@" or "#") render as-is.
                    if (window.FB && FB.icons && FB.icons[it.icon]) {
                        icon.innerHTML = FB.icon(it.icon);
                    } else {
                        icon.textContent = it.icon;
                    }

                    var main = document.createElement('span');
                    main.className = 'palette-item-main';
                    var title = document.createElement('span');
                    title.className = 'palette-item-title';
                    title.innerHTML = highlight(it.title, input.value.trim());
                    main.appendChild(title);
                    if (it.sub) {
                        var sub = document.createElement('span');
                        sub.className = 'palette-item-sub';
                        sub.textContent = it.sub;
                        main.appendChild(sub);
                    }

                    btn.appendChild(icon);
                    btn.appendChild(main);

                    if (it.count) {
                        var count = document.createElement('span');
                        count.className = 'palette-item-count';
                        count.textContent = it.count;
                        btn.appendChild(count);
                    }

                    btn.dataset.index = String(flat.length);
                    btn.addEventListener('click', function() {
                        selectItem(items[parseInt(btn.dataset.index, 10)]);
                    });
                    section.appendChild(btn);
                    flat.push(it);
                });

                results.appendChild(section);
            });

            // items now reflects only what's actually rendered (capped per
            // group), in DOM order, so keyboard navigation stays in sync.
            items = flat;
            setActive(0);
        }

        var elList = [];
        function setActive(index) {
            elList = Array.prototype.slice.call(results.querySelectorAll('.palette-item'));
            if (!elList.length) { activeIndex = -1; return; }
            if (index < 0) index = 0;
            if (index >= elList.length) index = elList.length - 1;
            activeIndex = index;
            elList.forEach(function(el, i) {
                el.classList.toggle('active', i === index);
                el.setAttribute('aria-selected', i === index ? 'true' : 'false');
            });
            var active = elList[activeIndex];
            if (active && active.scrollIntoView) {
                active.scrollIntoView({ block: 'nearest' });
            }
        }

        // ---- Actions --------------------------------------------------------
        function selectItem(it) {
            if (!it) {
                // No match: go to the full search page with the current query.
                var q = input.value.trim();
                window.location.href = '/search?q=' + encodeURIComponent(q) + '&tab=all';
                return;
            }
            if (it.action === 'navigate') {
                window.location.href = it.href;
            }
        }

        function openPalette() {
            if (isOpen) return;
            isOpen = true;
            lastFocused = document.activeElement;
            overlay.hidden = false;
            overlay.setAttribute('aria-hidden', 'false');
            dialog.setAttribute('aria-hidden', 'false');
            if (trigger) trigger.setAttribute('aria-expanded', 'true');
            document.body.classList.add('palette-open');
            input.value = '';
            items = [];
            renderPlaceholder();
            // Focus on next frame so the overlay is laid out first.
            requestAnimationFrame(function() { input.focus(); });
        }

        function renderPlaceholder() {
            results.innerHTML = '';
            var empty = document.createElement('div');
            empty.className = 'palette-empty';
            empty.textContent = 'Type to search across the fediverse.';
            results.appendChild(empty);
            activeIndex = -1;
            elList = [];
        }

        function closePalette() {
            if (!isOpen) return;
            isOpen = false;
            overlay.hidden = true;
            overlay.setAttribute('aria-hidden', 'true');
            dialog.setAttribute('aria-hidden', 'true');
            if (trigger) trigger.setAttribute('aria-expanded', 'false');
            document.body.classList.remove('palette-open');
            if (lastFocused && typeof lastFocused.focus === 'function') {
                lastFocused.focus();
            }
        }

        function isPaletteOpen() { return isOpen; }
        window.FB.paletteOpen = isPaletteOpen;

        // ---- Fetch ----------------------------------------------------------
        function search(query) {
            var mySeq = ++seq;
            inFlight++;
            fetch('/search/json?q=' + encodeURIComponent(query), {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'Accept': 'application/json' }
            })
                .then(function(r) { return r.ok ? r.json() : null; })
                .then(function(data) {
                    inFlight--;
                    if (mySeq !== seq) return;   // stale response
                    items = buildItems(data, query);
                    render();
                })
                .catch(function() {
                    inFlight--;
                    if (mySeq !== seq) return;
                    items = [];
                    render();
                });
        }

        // ---- Events ---------------------------------------------------------
        input.addEventListener('input', function() {
            clearTimeout(debounceTimer);
            var q = input.value.trim();
            if (q.length < MIN_CHARS) {
                seq++;
                items = [];
                renderPlaceholder();
                return;
            }
            // Render optimistic substring filter immediately, then refine.
            debounceTimer = setTimeout(function() { search(q); }, DEBOUNCE_MS);
        });

        input.addEventListener('keydown', function(e) {
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                setActive(activeIndex + 1);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                setActive(activeIndex - 1);
            } else if (e.key === 'Enter') {
                e.preventDefault();
                var chosen = activeIndex >= 0 && activeIndex < items.length ? items[activeIndex] : null;
                selectItem(chosen);
            } else if (e.key === 'Escape') {
                e.preventDefault();
                closePalette();
            }
        });

        // Click on the overlay backdrop closes the palette.
        overlay.addEventListener('mousedown', function(e) {
            if (e.target === overlay) closePalette();
        });

        // Focus trap: while the modal is open, Tab/Shift+Tab cycle within it so
        // focus can't escape behind the aria-modal dialog (WCAG 2.4.3).
        dialog.addEventListener('keydown', function(e) {
            if (e.key !== 'Tab' || !isOpen) return;
            var focusables = dialog.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), [tabindex]:not([tabindex="-1"])'
            );
            if (!focusables.length) return;
            var first = focusables[0];
            var last = focusables[focusables.length - 1];
            var active = document.activeElement;
            if (e.shiftKey && active === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && active === last) {
                e.preventDefault();
                first.focus();
            }
        });

        // Trigger button.
        if (trigger) {
            trigger.addEventListener('click', function(e) {
                e.preventDefault();
                openPalette();
            });
        }

        // Global shortcut: Ctrl+K / Cmd+K toggles the palette.
        document.addEventListener('keydown', function(e) {
            if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
                e.preventDefault();
                if (isOpen) closePalette();
                else openPalette();
            }
        });

        renderPlaceholder();
    });
})();
