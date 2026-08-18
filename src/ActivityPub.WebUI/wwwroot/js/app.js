(function() {
    'use strict';

    var FB = {
        modules: {},
        ready: false,

        register: function(name, fn) {
            this.modules[name] = fn;
            if (this.ready) this.run(name);
            return this;
        },

        run: function(name) {
            var fn = this.modules[name];
            if (!fn) return;
            delete this.modules[name];
            try {
                fn();
            } catch (err) {
                console.error('[fediblog] module "' + name + '" failed:', err);
            }
        },

        ready: function() {
            if (this.ready) return;
            this.ready = true;
            var pending = this.modules;
            this.modules = {};
            for (var name in pending) this.run(name);
        },

        decodeUrl: function(v) {
            return v ? v.replace(/&amp;/g, '&') : v;
        },

        timeAgo: function(date) {
            var diff = Date.now() - date.getTime();
            var mins = Math.floor(diff / 60000);
            if (mins < 1) return 'just now';
            if (mins < 60) return mins + 'm ago';
            var hrs = Math.floor(mins / 60);
            if (hrs < 24) return hrs + 'h ago';
            var days = Math.floor(hrs / 24);
            if (days < 30) return days + 'd ago';
            return date.toLocaleDateString();
        },

        updateTimestamps: function() {
            // Any element carrying a machine-readable published timestamp is
            // refreshed to a relative "Xm ago" form. Covers both timeline note
            // cards (.note-timestamp) and notification list items.
            document.querySelectorAll('[data-published]').forEach(function(el) {
                var d = new Date(el.dataset.published);
                if (isNaN(d.getTime())) return;
                el.textContent = FB.timeAgo(d);
            });
        }
    };
    window.FB = FB;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function() { FB.ready(); });
    } else {
        FB.ready();
    }

    // ---- Toasts -----------------------------------------------------------
    FB.register('toast', function() {
        var container = document.getElementById('toast-container');
        if (!container) return;

        var DURATION = 4000;

        window.showToast = function(message, type) {
            if (!message) return;
            type = type || 'info';

            var toast = document.createElement('div');
            toast.className = 'toast toast-' + type;
            toast.setAttribute('role', 'status');

            var icon = type === 'success' ? '✓' : type === 'error' ? '✕' : type === 'warning' ? '⚠' : 'ℹ';
            // Icon is decorative (aria-hidden) so screen readers announce only
            // the message; the container's role="status" live region does the rest.
            toast.innerHTML = '<span class="toast-icon" aria-hidden="true">' + icon + '</span><span class="toast-msg"></span>';
            toast.querySelector('.toast-msg').textContent = message;

            container.appendChild(toast);

            requestAnimationFrame(function() {
                toast.classList.add('toast-visible');
            });

            var timeout = setTimeout(function() {
                dismiss(toast);
            }, DURATION);

            toast.addEventListener('click', function() {
                clearTimeout(timeout);
                dismiss(toast);
            });

            function dismiss(el) {
                el.classList.remove('toast-visible');
                el.classList.add('toast-hidden');
                setTimeout(function() {
                    if (el.parentNode) el.parentNode.removeChild(el);
                }, 300);
            }
        };
    });

    // ---- Theme toggle -----------------------------------------------------
    FB.register('theme', function() {
        function currentTheme() {
            return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
        }
        function paint(btn) {
            if (!btn) return;
            var icon = btn.querySelector('.theme-toggle-icon');
            if (icon) icon.textContent = currentTheme() === 'dark' ? '☀' : '☾';
            btn.setAttribute('aria-pressed', currentTheme() === 'dark' ? 'true' : 'false');
            btn.setAttribute('aria-label', currentTheme() === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
        }
        var btn = document.getElementById('theme-toggle');
        paint(btn);
        if (btn) {
            btn.addEventListener('click', function() {
                var next = currentTheme() === 'dark' ? 'light' : 'dark';
                document.documentElement.setAttribute('data-theme', next);
                try { localStorage.setItem('fediblog-theme', next); } catch (e) {}
                paint(btn);
            });
        }
    });

    // ---- Global keyboard shortcuts -----------------------------------------
    FB.register('shortcuts', function() {
        document.addEventListener('keydown', function(e) {
            var tag = (document.activeElement && document.activeElement.tagName) || '';
            if (tag === 'INPUT' || tag === 'TEXTAREA' || (document.activeElement && document.activeElement.isContentEditable)) return;
            if (e.key === '/') {
                e.preventDefault();
                var s = document.getElementById('nav-search-input');
                if (s) s.focus();
            } else if (e.key === 'n') {
                e.preventDefault();
                window.location.href = '/Compose/Index';
            }
        });
    });

    // ---- Live timeline (SignalR primary, SSE fallback) ---------------------
    // Shared by both transports: receives a NewActivity payload and, when the
    // timeline page is open, fetches + prepends the server-rendered note card.
    FB.register('live-timeline', function() {
        var currentUser = document.body.getAttribute('data-current-user') || '';
        var feed = document.querySelector('.timeline-feed[data-feed]');
        if (!feed) return; // not a timeline page — nothing to prepend into

        var liveRegion = document.createElement('div');
        liveRegion.className = 'sr-only';
        liveRegion.setAttribute('aria-live', 'polite');
        liveRegion.setAttribute('role', 'status');
        document.body.appendChild(liveRegion);

        function isOnTimeline() {
            return !!(document.querySelector('.timeline-feed[data-feed]'));
        }

        function handleNewActivity(data) {
            if (!data || !data.ActivityId) return;

            // Self-echo suppression: the card for a note we just composed is
            // already in the DOM (ComposeController redirects back to a page
            // that re-renders it), so don't bump the badge or prepend a dup.
            var isSelf = data.ActorName && currentUser &&
                (data.ActorName === currentUser || data.ActorName === '@' + currentUser);
            if (isSelf) {
                console.log('[LiveTimeline] Ignoring own activity:', data.ActivityId);
                return;
            }

            // De-dupe: skip if the card is already rendered (e.g. a late
            // duplicate broadcast after a reload that already fetched it).
            if (document.querySelector('.note-card[data-activity-id="' + CSS.escape(data.ActivityId) + '"]')) {
                return;
            }

            if (!isOnTimeline()) {
                console.log('[LiveTimeline] Not on timeline; skipping prepend for', data.ActivityId);
                return;
            }

            fetch('/timeline/card/' + encodeURIComponent(data.ActivityId), {
                credentials: 'same-origin',
                headers: { 'Accept': 'text/html' }
            })
                .then(function(r) { return r.ok ? r.text() : null; })
                .then(function(html) {
                    if (!html) return;
                    var wrap = document.createElement('div');
                    wrap.innerHTML = html;
                    var card = wrap.querySelector('.note-card');
                    if (!card) return;

                    feed.insertBefore(card, feed.firstChild);
                    card.classList.add('note-card-new');
                    var reduceMotion = window.matchMedia &&
                        window.matchMedia('(prefers-reduced-motion: reduce)').matches;
                    card.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'start' });
                    setTimeout(function () { card.classList.remove('note-card-new'); }, 4000);

                    var author = card.querySelector('.note-username');
                    var contentEl = card.querySelector('.note-content');
                    liveRegion.textContent = 'New post by ' +
                        (author ? author.textContent : 'someone') +
                        ': ' + (contentEl ? contentEl.textContent.slice(0, 80) : '');
                    console.log('[LiveTimeline] Prepended', data.ActivityId);
                })
                .catch(function (err) { console.warn('[LiveTimeline] fetch failed:', err); });
        }

        // Expose for the SignalR + SSE transports to call.
        window.FB.handleLiveActivity = handleNewActivity;
    });

    // ---- SignalR notifications (single bootstrap for the whole app) --------
    FB.register('signals', function() {
        if (typeof signalR === 'undefined') return;
        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/notifications/ws')
            .withAutomaticReconnect()
            .build();
        window.fbSignalR = { connection: connection, ok: false };

        var notificationCount = 0;
        var badgeInitialized = false;
        var badge = document.getElementById('notification-badge');
        var soundEnabled = true;
        var desktopEnabled = false;
        var maxConnections = 5;
        var notificationSound = new Audio();
        notificationSound.src = 'data:audio/wav;base64,UklGRnoGAABXQVZFZm10IBAAAAABAAEAQB8AAEAfAAABAAgAZGF0YQoGAACBhYqFbF1fdH2LkZeVe5CTm3+UlZl+eX+Yd2V9c3F6dHByeG97b3RxcnpxdHJ5bXpvdm91b3BvcG5vbG9vbW5uc25sbmxrbm1vbWxrbGttbGxsa2xrbGtsbGtsbWxsbWxsa21sbGtsbWxtbW1sbm1tbWxtbW1tbm5tbm5ucXFucm1wb25ybXNycm5sbm1ubm1sc3JybHRycmxscnJrbG5ycnRsbG1sc3JybXJscnRsbG1scnJrbG5ycnRsa21sbW5ubW5ucnRrbW5ubW5ucm5yc3Zsc3Zrbm5yd3Rrdm5zd21tc3Vvc3VvbG1yc3Vuc3Vvc3RrbW1yc3RvcXZuc3NvbW1yc3VvcnVvc3NvbW5zc3ZvcnZvc3RvbW9zc3Zuc3Zvc3RvbW9zc3dvcndvc3RvbW9zc3dvcndvc3RvbW90c3dvcndvc3RvbW90c3dvcndvc3Q=';

        // Seed the badge from the server's persisted unread count so it
        // survives page reloads (instead of resetting to 0 each load).
        fetch('/notifications/badge')
            .then(function(r) { return r.ok ? r.json() : {}; })
            .then(function(data) {
                if (data && typeof data.count === 'number') {
                    notificationCount = data.count;
                    renderBadge();
                    badgeInitialized = true;
                }
            })
            .catch(function() {});

        fetch('/RealtimeSettings/get')
            .then(function(r) { return r.ok ? r.json() : {}; })
            .then(function(settings) {
                if (settings) {
                    soundEnabled = settings.notificationSoundEnabled !== false;
                    desktopEnabled = settings.desktopNotificationsEnabled || false;
                    maxConnections = settings.maxConnections || 5;
                }
            })
            .catch(function() {});

        function renderBadge() {
            if (!badge) return;
            if (notificationCount > 0) {
                badge.textContent = notificationCount > 99 ? '99+' : String(notificationCount);
                badge.style.display = 'inline';
            } else {
                badge.textContent = '0';
                badge.style.display = 'none';
            }
        }

        function bumpBadge() {
            notificationCount++;
            renderBadge();
        }

        connection.on('NewActivity', function(data) {
            // Only bump the badge / sound / desktop notif; the live-timeline
            // module independently prepends the card when on the timeline page.
            if (data && data.ActorName && document.body.getAttribute('data-current-user') === data.ActorName) {
                // own post already in DOM — skip the "new" noise
            } else {
                bumpBadge();
                playNotificationSound();
                sendDesktopNotification('New ' + data.Type, data.Content || '', data.ActorName);
            }
            if (window.FB.handleLiveActivity) window.FB.handleLiveActivity(data);
            console.log('[SignalR] New activity:', data.Type, 'by', data.ActorName);
        });

        connection.on('NewNotification', function(data) {
            bumpBadge();
            playNotificationSound();
            var title = (data.Type || 'Notification').charAt(0).toUpperCase() + (data.Type || 'notification').slice(1);
            var body = (data.ActorName ? data.ActorName + ' — ' : '') + (data.Message || '');
            // Deep link: open the notifications page (the badge already
            // reflects the new item); the user can jump to the source note
            // from there.
            sendDesktopNotification(title, body, data.Link || '/notifications');
            console.log('[SignalR] Notification:', data.Type, data.Message);
        });

        connection.on('Connected', function(username) {
            console.log('[SignalR] Connected as', username);
        });

        function playNotificationSound() {
            if (!soundEnabled) return;
            try { notificationSound.play().catch(function() {}); } catch (e) {}
        }

        function sendDesktopNotification(title, body, actorName, link) {
            if (!desktopEnabled) return;
            if (!('Notification' in window)) return;
            function show(t, b) {
                var n = new Notification(t, { body: b });
                if (link) {
                    n.onclick = function() {
                        try { window.focus(); window.location.href = link; n.close(); } catch (e) {}
                    };
                }
            }
            if (Notification.permission === 'granted') {
                show(title + (actorName ? ' by ' + actorName : ''), body);
            } else if (Notification.permission !== 'denied') {
                Notification.requestPermission().then(function(permission) {
                    if (permission === 'granted') show(title, body);
                });
            }
        }

        connection.start()
            .then(function () { window.fbSignalR.ok = true; })
            .catch(function (err) {
                window.fbSignalR.ok = false;
                console.log('[SignalR] Connection failed:', err);
            });
    });

    // ---- SSE fallback transport (only when SignalR is unavailable) ----------
    // Consumes /timeline/events and feeds the same live-timeline handler.
    // Kept in its own module so it never double-fires alongside SignalR.
    FB.register('sse-fallback', function () {
        if (typeof EventSource === 'undefined') return;
        // /timeline/events is only authorized for signed-in users and only
        // useful where a feed is open. Without these guards an anonymous
        // visitor would 401 forever (EventSource auto-retries) and every
        // non-timeline page would carry a useless open stream.
        if (!document.body.getAttribute('data-current-user')) return;
        if (!document.querySelector('.timeline-feed[data-feed]')) return;

        var started = false;
        function startSSE() {
            if (started) return;
            // Only fall back when there is no live SignalR connection.
            if (window.fbSignalR && window.fbSignalR.ok) {
                console.log('[SSE] SignalR active; SSE fallback disabled');
                return;
            }
            started = true;
            console.log('[SSE] Starting fallback stream');
            var src = new EventSource('/timeline/events');
            src.addEventListener('new_activity', function (e) {
                try {
                    var data = JSON.parse(e.data);
                    // Normalize to the SignalR-style payload the handler expects.
                    if (window.FB.handleLiveActivity) {
                        window.FB.handleLiveActivity({
                            ActivityId: data.activityId,
                            Type: data.type,
                            ActorName: data.actorName,
                            Content: data.content,
                            Timestamp: data.timestamp
                        });
                    }
                } catch (err) { console.warn('[SSE] bad event:', err); }
            });
            src.onerror = function () {
                // EventSource auto-reconnects; if the stream is long-dead,
                // let it keep retrying (built-in) rather than tearing down.
                console.log('[SSE] connection error (auto-retrying)');
            };
        }

        // Give SignalR a moment to establish before deciding to fall back.
        setTimeout(function () {
            // fbSignalR.ok is only true once .start() resolved. If SignalR is
            // absent (module early-returned) ok stays undefined → fall back.
            startSSE();
        }, 1500);
    });

    // ---- Relative timestamps ----------------------------------------------
    FB.register('timestamps', function() {
        if (!document.querySelector('[data-published]')) return;
        FB.updateTimestamps();
        setInterval(FB.updateTimestamps, 60000);
    });

    // ---- Auto mark-as-read on the notifications page -----------------------
    // When the user opens /notifications, the persisted unread cursor advances
    // to "now" and the nav badge clears — so the count reflects what they've
    // actually seen, not just what arrived during this session.
    FB.register('notifications-read', function() {
        if (!document.body.classList.contains('page-notifications')) return;
        // Defer a tick so the page has rendered (and any late items are in).
        setTimeout(function() {
            fetch('/notifications/markallread', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: 'RequestVerificationToken=' + encodeURIComponent(getAntiForgeryToken())
            })
            .then(function(r) {
                if (!r.ok) return;
                var badge = document.getElementById('notification-badge');
                if (badge) { badge.textContent = '0'; badge.style.display = 'none'; }
            })
            .catch(function() {});
        }, 250);
    });

    function getAntiForgeryToken() {
        var el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    // ---- Deep link: scroll to a specific note on the timeline --------------
    // /timeline?note=<activityId> scrolls to and flashes the matching card.
    FB.register('deep-link-note', function() {
        var params = new URLSearchParams(window.location.search);
        var noteId = params.get('note');
        if (!noteId) return;
        function scrollToNote() {
            var card = document.querySelector('.note-card[data-activity-id="' + noteId + '"]');
            if (card) {
                card.scrollIntoView({ behavior: 'smooth', block: 'center' });
                card.classList.add('note-flash');
                setTimeout(function() { card.classList.remove('note-flash'); }, 1600);
                return true;
            }
            return false;
        }
        // The note may not be on the first page yet; retry briefly while the
        // feed renders, then give up.
        var attempts = 0;
        var timer = setInterval(function() {
            if (scrollToNote() || ++attempts > 10) clearInterval(timer);
        }, 300);
    });

    // ---- Client-side "load more" / infinite scroll -------------------------
    FB.register('loadmore', function() {
        document.querySelectorAll('.load-more-container[data-next]').forEach(function(container) {
            var btn = container.querySelector('.load-more-btn');
            if (!btn) return;
            var feed = container.closest('[data-feed]');
            var sibling = container.nextElementSibling;
            var skeleton = sibling && sibling.id === 'load-more-skeleton'
                ? sibling
                : document.getElementById('load-more-skeleton');
            var cardSel = feed ? (feed.dataset.cardSelector || '') : '';
            if (!feed || !cardSel || !skeleton) return;

            var loading = false;
            var nextUrl = FB.decodeUrl(container.getAttribute('data-next'));

            function setLoading(state) {
                loading = state;
                skeleton.classList.toggle('active', state);
                btn.disabled = state;
                btn.textContent = state ? 'Loading…' : 'Load more';
            }

            function inject(html) {
                var doc = new DOMParser().parseFromString(html, 'text/html');
                var newFeed = doc.querySelector('[data-feed]');
                if (!newFeed) return;
                var existing = new Set();
                feed.querySelectorAll(cardSel + '[data-activity-id]').forEach(function(c) {
                    existing.add(c.getAttribute('data-activity-id'));
                });
                var frag = document.createDocumentFragment();
                newFeed.querySelectorAll(cardSel).forEach(function(card) {
                    var id = card.getAttribute('data-activity-id');
                    if (id && existing.has(id)) return;
                    if (id) existing.add(id);
                    frag.appendChild(card.cloneNode(true));
                });
                if (frag.childNodes.length) feed.appendChild(frag);

                var next = doc.querySelector('.load-more-container');
                var noMore = !doc.querySelector('.load-more-btn');
                if (next && next.getAttribute('data-next')) {
                    var decodedNext = FB.decodeUrl(next.getAttribute('data-next'));
                    container.setAttribute('data-page', next.getAttribute('data-page'));
                    container.setAttribute('data-next', next.getAttribute('data-next'));
                    nextUrl = decodedNext;
                    btn.href = decodedNext;
                    if (feed.hasAttribute('data-page')) {
                        feed.setAttribute('data-page', next.getAttribute('data-page'));
                        feed.setAttribute('data-next', next.getAttribute('data-next'));
                    }
                    FB.updateTimestamps();
                } else if (noMore) {
                    container.remove();
                    if (skeleton) skeleton.remove();
                }
            }

            async function loadMore(e) {
                if (e) e.preventDefault();
                if (loading) return;
                if (!nextUrl) return;
                setLoading(true);
                try {
                    var res = await fetch(nextUrl, { credentials: 'same-origin' });
                    if (!res.ok) throw new Error('load failed');
                    inject(await res.text());
                } catch (err) {
                    showToast('Could not load more', 'error');
                } finally {
                    setLoading(false);
                }
            }

            btn.addEventListener('click', loadMore);
            if ('IntersectionObserver' in window) {
                var observer = new IntersectionObserver(function(entries) {
                    if (entries[0] && entries[0].isIntersecting) loadMore(null);
                }, { rootMargin: '600px 0px' });
                observer.observe(skeleton);
            }
        });
    });

    // ---- Note card interactions (like/boost/delete/reply/copy) -------------
    FB.register('notes', function() {
        // Optimistic like/boost with server-authoritative reconciliation.
        //
        // A single delegated submit handler is attached to the document for all
        // .btn-like / .btn-boost forms. Because reconciliation splices fresh
        // server-rendered buttons (and their forms) into the live DOM, a
        // delegate is the only binding that keeps working for dynamically
        // inserted cards without accumulating duplicate handlers.
        //
        // On submit we:
        //   1. Optimistically toggle the active state + count by one.
        //   2. Fire the POST.
        //   3. On success, reconcile the button's count + active state with the
        //      authoritative server fragment at /timeline/card/<id> so a stale
        //      or concurrent page self-corrects.
        //   4. On failure, roll the button back to its captured original HTML.
        document.addEventListener('submit', function(e) {
            var btn = e.target && e.target.closest
                ? e.target.closest('.btn-like, .btn-boost')
                : null;
            if (!btn || !e.target.matches || !e.target.matches('form')) return;
            e.preventDefault();
            if (btn.disabled) return;

            var form = e.target;
            var noteCard = btn.closest('.note-card');
            var activityId = noteCard ? noteCard.getAttribute('data-activity-id') : null;
            var isLike = btn.classList.contains('btn-like');
            var icon = isLike ? '♥' : '↻';

            // Capture the original button markup for rollback.
            var origHtml = btn.outerHTML;

            // Optimistic update: toggle the active state and adjust the count.
            var text = btn.textContent;
            var parts = text.split(/\s+/);
            var count = parseInt(parts[parts.length - 1], 10) || 0;
            var isActive = btn.classList.contains('active');
            var newCount = Math.max(0, isActive ? count - 1 : count + 1);
            btn.textContent = icon + ' ' + newCount;
            btn.classList.toggle('active', !isActive);
            btn.disabled = true;

            var fd = new FormData(form);
            fetch(form.action, { method: 'POST', body: fd, credentials: 'same-origin' })
                .then(function(r) {
                    if (!r.ok) throw new Error('Failed');
                    var msg = isLike ? (isActive ? 'Unlike successful' : 'Liked') : (isActive ? 'Unboosted' : 'Boosted');
                    showToast(msg, 'success');

                    // Reconcile with the authoritative server state.
                    if (activityId && window.FB.reconcileNoteCard) {
                        return window.FB.reconcileNoteCard(activityId, btn, noteCard, isLike);
                    }
                    btn.disabled = false;
                })
                .catch(function() {
                    // Roll back the optimistic change.
                    if (noteCard) {
                        var fresh = document.createElement('div');
                        fresh.innerHTML = origHtml;
                        var restored = fresh.querySelector('button');
                        if (restored && btn.parentNode) {
                            btn.parentNode.replaceChild(restored, btn);
                        }
                    }
                    if (btn && btn.parentNode) btn.disabled = false;
                    showToast('Action failed. Please try again.', 'error');
                });
        }, true);

        // Reconcile a note card's like/boost button with the server-rendered
        // fragment at /timeline/card/<id>. The fragment is the single source of
        // truth for counts + active state; we splice the fresh button back into
        // the live card so the optimistic mutation is corrected if it diverged
        // from the server (e.g. a concurrent like from another client).
        window.FB.reconcileNoteCard = function(activityId, currentBtn, noteCard, isLike) {
            return fetch('/timeline/card/' + encodeURIComponent(activityId), { credentials: 'same-origin' })
                .then(function(r) { return r.ok ? r.text() : null; })
                .then(function(html) {
                    if (!html || !noteCard) {
                        if (currentBtn) currentBtn.disabled = false;
                        return;
                    }
                    var doc = new DOMParser().parseFromString(html, 'text/html');
                    var freshCard = doc.querySelector('.note-card[data-activity-id="' + activityId + '"]');
                    if (!freshCard) {
                        if (currentBtn) currentBtn.disabled = false;
                        return;
                    }
                    var freshBtn = freshCard.querySelector(isLike ? '.btn-like' : '.btn-boost');
                    if (freshBtn && currentBtn.parentNode) {
                        currentBtn.parentNode.replaceChild(freshBtn, currentBtn);
                        syncNoteButtons(noteCard, isLike ? '.btn-like' : '.btn-boost');
                    }
                    if (currentBtn) currentBtn.disabled = false;
                })
                .catch(function() {
                    // Reconciliation is best-effort; leave the optimistic
                    // value in place (it's already a reasonable approximation).
                    if (currentBtn) currentBtn.disabled = false;
                });
        };

        // Keep the like/boost buttons in a card visually in sync (the server
        // renders each independently; after a splice we mirror the fresh value
        // onto its sibling so the card doesn't show two different counts).
        function syncNoteButtons(card, selector) {
            var buttons = card.querySelectorAll(selector);
            if (buttons.length < 2) return;
            var first = buttons[0];
            var text = first.textContent;
            var active = first.classList.contains('active');
            buttons.forEach(function(b) {
                if (b === first) return;
                b.textContent = text;
                b.classList.toggle('active', active);
            });
        }

        document.querySelectorAll('.btn-delete').forEach(function(btn) {
            var form = btn.closest('form');
            if (!form) return;
            form.addEventListener('submit', function(e) {
                if (!confirm('Are you sure you want to delete this post? This cannot be undone.')) {
                    e.preventDefault();
                    return;
                }
                var noteCard = btn.closest('.note-card');
                var fd = new FormData(form);
                fetch(form.action, { method: 'POST', body: fd, credentials: 'same-origin' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('Failed');
                        showToast('Post deleted', 'success');
                        if (noteCard) {
                            noteCard.style.transition = 'opacity 0.3s, transform 0.3s';
                            noteCard.style.opacity = '0';
                            noteCard.style.transform = 'translateX(20px)';
                            setTimeout(function() { noteCard.remove(); }, 300);
                        }
                    })
                    .catch(function() {
                        showToast('Delete failed. Please try again.', 'error');
                    });
            });
        });

        document.querySelectorAll('.reply-form-container form').forEach(function(form) {
            form.addEventListener('submit', function(e) {
                var fd = new FormData(form);
                fetch(form.action, { method: 'POST', body: fd, credentials: 'same-origin' })
                    .then(function(r) {
                        if (!r.ok) throw new Error('Failed');
                        showToast('Reply posted', 'success');
                        var textarea = form.querySelector('textarea');
                        if (textarea) textarea.value = '';
                    })
                    .catch(function() {
                        showToast('Reply failed. Please try again.', 'error');
                    });
            });
        });

        document.querySelectorAll('.btn-reply[data-reply-target]').forEach(function(btn) {
            btn.addEventListener('click', function(e) {
                e.preventDefault();
                var target = document.getElementById(btn.dataset.replyTarget);
                if (!target) return;
                var isOpen = !target.hidden;
                document.querySelectorAll('.reply-form-container[hidden]').forEach(function(c) { c.hidden = false; });
                document.querySelectorAll('.reply-form-container').forEach(function(c) {
                    if (c !== target) c.hidden = true;
                });
                target.hidden = isOpen;
                if (!target.hidden) {
                    var textarea = target.querySelector('textarea');
                    if (textarea) textarea.focus();
                }
            });
        });

        document.querySelectorAll('.note-more-menu').forEach(function(menu) {
            var btn = menu.querySelector('.btn-more');
            var dropdown = menu.querySelector('.note-more-dropdown');
            if (!btn || !dropdown) return;

            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                var isOpen = btn.getAttribute('aria-expanded') === 'true';
                closeAllMoreMenus();
                btn.setAttribute('aria-expanded', String(!isOpen));
                dropdown.classList.toggle('open', !isOpen);
            });

            dropdown.querySelectorAll('[data-action="copy-link"]').forEach(function(item) {
                item.addEventListener('click', function() {
                    var url = item.dataset.url || window.location.origin;
                    navigator.clipboard.writeText(url).then(function() {
                        showToast('Link copied to clipboard', 'success');
                    }).catch(function() {
                        showToast('Failed to copy link', 'error');
                    });
                    closeAllMoreMenus();
                });
            });

            dropdown.querySelectorAll('a').forEach(function(link) {
                link.addEventListener('click', function() {
                    closeAllMoreMenus();
                });
            });

            dropdown.querySelectorAll('form').forEach(function(form) {
                form.addEventListener('submit', function() {
                    closeAllMoreMenus();
                });
            });
        });

        function closeAllMoreMenus() {
            document.querySelectorAll('.note-more-menu').forEach(function(m) {
                var b = m.querySelector('.btn-more');
                var d = m.querySelector('.note-more-dropdown');
                if (b) b.setAttribute('aria-expanded', 'false');
                if (d) d.classList.remove('open');
            });
        }

        document.addEventListener('click', closeAllMoreMenus);

        document.querySelectorAll('.cw-toggle-btn').forEach(function(btn) {
            var noteCard = btn.closest('.note-card');
            if (noteCard && btn.getAttribute('aria-expanded') !== 'true') {
                noteCard.classList.add('cw-hidden');
            }
            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                if (!noteCard) return;
                var isExpanded = btn.getAttribute('aria-expanded') === 'true';
                noteCard.classList.toggle('cw-hidden', isExpanded);
                btn.textContent = isExpanded ? 'Show' : 'Hide';
                btn.setAttribute('aria-expanded', String(!isExpanded));
            });
        });
    });

    // ---- Optimistic follow/unfollow (profile page) --------------------------
    // The Follow/Unfollow button on a profile toggles immediately, fires the
    // POST, then reconciles the authoritative state (button + follower count)
    // from /Profile/State. On failure the button is rolled back.
    //
    // init is stored on window.FB so a rollback can re-run it against the
    // restored button (the form-level submit handler is bound once per form,
    // so re-invoking init on a fresh button would not re-attach it).
    function initFollowToggle() {
        var btn = document.querySelector('.btn-follow-state');
        if (!btn) return;
        var form = btn.closest('form');
        if (!form) return;

        var targetUsername = (form.querySelector('input[name="target-username"]') || {}).value || '';
        var isFollowing = btn.classList.contains('btn-secondary') && /following/i.test(btn.textContent);
        var origHtml = btn.outerHTML;
        var formAction = form.action;

        function paintBtn(following, count) {
            btn.textContent = following ? 'Following' : 'Follow';
            btn.classList.toggle('btn-secondary', following);
            btn.classList.toggle('btn-primary', !following);
            if (count != null) updateFollowerCount(count);
        }

        function updateFollowerCount(count) {
            // The profile shows "N Followers" in .profile-stats; update the
            // number so the optimistic state is reflected without a reload.
            var stats = document.querySelector('.profile-stats');
            if (!stats) return;
            var stat = Array.prototype.slice.call(stats.querySelectorAll('.stat')).find(function(el) {
                return /followers/i.test(el.textContent);
            });
            if (stat) {
                var strong = stat.querySelector('strong');
                if (strong) strong.textContent = String(count);
            }
        }

        form.addEventListener('submit', function(e) {
            e.preventDefault();
            if (btn.disabled) return;

            var willFollow = !isFollowing;
            btn.disabled = true;

            // Optimistic toggle.
            paintBtn(willFollow);

            var fd = new FormData(form);
            fetch(formAction, { method: 'POST', body: fd, credentials: 'same-origin' })
                .then(function(r) {
                    if (!r.ok) throw new Error('Failed');
                    showToast(willFollow ? 'Now following ' + targetUsername : 'Unfollowed ' + targetUsername, 'success');
                    isFollowing = willFollow;

                    // Reconcile with the authoritative server state.
                    if (targetUsername && window.FB.reconcileFollowState) {
                        return window.FB.reconcileFollowState(targetUsername, function(following, count) {
                            isFollowing = following;
                            paintBtn(following, count);
                        }, function() { btn.disabled = false; });
                    }
                    btn.disabled = false;
                })
                .catch(function() {
                    // Roll back: restore the original button markup, then
                    // re-init the toggle against it so the form submit handler
                    // works again (it's bound to the form, so a fresh button
                    // needs init re-run to re-capture state + re-attach).
                    var fresh = document.createElement('div');
                    fresh.innerHTML = origHtml;
                    var restored = fresh.querySelector('button');
                    if (restored && btn.parentNode) {
                        btn.parentNode.replaceChild(restored, btn);
                        if (window.FB.initFollowToggle) window.FB.initFollowToggle();
                    }
                    showToast('Action failed. Please try again.', 'error');
                });
        });
    }
    window.FB.initFollowToggle = initFollowToggle;
    FB.register('follow-toggle', initFollowToggle);

    // Reconcile follow-state with the server and apply it (button + count).
    // apply(following, count) updates the UI; done() re-enables the button.
    window.FB.reconcileFollowState = function(targetUsername, apply, done) {
        return fetch('/Profile/State?username=' + encodeURIComponent(targetUsername), { credentials: 'same-origin' })
            .then(function(r) { return r.ok ? r.json() : null; })
            .then(function(state) {
                if (!state) {
                    if (done) done();
                    return;
                }
                apply(state.isFollowing, state.followerCount);
                if (done) done();
            })
            .catch(function() {
                if (done) done();
            });
    };

    // ---- Optimistic community join/leave ------------------------------------
    // The Join/Leave button on a community page toggles immediately, fires the
    // POST, then reconciles the authoritative state (button + member count)
    // from the re-fetched community page. On failure the button is rolled back.
    function initCommunityToggle() {
        var btn = document.querySelector('.community-actions form.inline-form button[type="submit"]');
        if (!btn) return;
        var form = btn.closest('form');
        if (!form) return;
        var communityId = (form.querySelector('input[name="communityId"]') || {}).value || '';
        var origHtml = btn.outerHTML;
        var formAction = form.action;
        var isMember = /leave/i.test(btn.textContent);

        function paintCommunityBtn(joined) {
            btn.textContent = joined ? 'Leave' : 'Join';
            btn.classList.toggle('btn-secondary', joined);
            btn.classList.toggle('btn-primary', !joined);
        }

        function updateMemberBadge(joined) {
            // Toggle the "You are a member" badge to reflect the new state.
            var details = document.querySelector('.community-details');
            if (!details) return;
            var existing = details.querySelector('.badge.badge-member');
            if (joined && !existing) {
                var meta = details.querySelector('.card-meta');
                if (!meta) return;
                var badge = document.createElement('span');
                badge.className = 'badge badge-member';
                badge.textContent = 'You are a member';
                meta.appendChild(badge);
            } else if (!joined && existing) {
                existing.remove();
            }
        }

        form.addEventListener('submit', function(e) {
            e.preventDefault();
            if (btn.disabled) return;

            var willJoin = !isMember;
            btn.disabled = true;

            // Optimistic toggle: swap button text/style + the member badge.
            paintCommunityBtn(willJoin);
            updateMemberBadge(willJoin);

            var fd = new FormData(form);
            fetch(formAction, { method: 'POST', body: fd, credentials: 'same-origin' })
                .then(function(r) {
                    if (!r.ok) throw new Error('Failed');
                    showToast(willJoin ? 'Joined community' : 'Left community', 'success');
                    isMember = willJoin;
                    btn.disabled = false;

                    // Reconcile the member count from the authoritative page.
                    if (communityId) {
                        fetch('/communities/show?communityId=' + encodeURIComponent(communityId), { credentials: 'same-origin', cache: 'no-store' })
                            .then(function(r2) { return r2.ok ? r2.text() : null; })
                            .then(function(html) {
                                if (!html) return;
                                var doc = new DOMParser().parseFromString(html, 'text/html');
                                var meta = doc.querySelector('.community-details .card-meta');
                                var live = document.querySelector('.community-details .card-meta');
                                if (meta && live) live.innerHTML = meta.innerHTML;
                            })
                            .catch(function() {});
                    }
                })
                .catch(function() {
                    // Roll back: restore the original button markup, then
                    // re-init so the form submit handler works again.
                    var fresh = document.createElement('div');
                    fresh.innerHTML = origHtml;
                    var restored = fresh.querySelector('button');
                    if (restored && btn.parentNode) {
                        btn.parentNode.replaceChild(restored, btn);
                        if (window.FB.initCommunityToggle) window.FB.initCommunityToggle();
                    }
                    showToast('Action failed. Please try again.', 'error');
                });
        });
    }
    window.FB.initCommunityToggle = initCommunityToggle;
    FB.register('community-toggle', initCommunityToggle);

    // ---- Generic dropdowns (nav groups + note more menus) ------------------
    FB.register('dropdowns', function() {
        if (!document.querySelector('.nav-group-toggle')) {
            var anyMore = document.querySelector('.note-more-menu');
            if (!anyMore) return;
        }

        function closeAllGroups(except) {
            document.querySelectorAll('.nav-group.open').forEach(function(g) {
                if (g !== except) {
                    g.classList.remove('open');
                    var btn = g.querySelector('.nav-group-toggle');
                    if (btn) btn.setAttribute('aria-expanded', 'false');
                }
            });
        }

        function closeAllMoreMenus() {
            document.querySelectorAll('.note-more-menu').forEach(function(m) {
                var b = m.querySelector('.btn-more');
                var d = m.querySelector('.note-more-dropdown');
                if (b) b.setAttribute('aria-expanded', 'false');
                if (d) d.classList.remove('open');
            });
        }

        function focusables(container) {
            return Array.prototype.slice.call(
                container.querySelectorAll('a[href], button:not([disabled]), [tabindex]:not([tabindex="-1"])')
            ).filter(function(el) {
                return el.offsetParent !== null || el === document.activeElement;
            });
        }

        function wireGroup(btn) {
            btn.addEventListener('click', function(e) {
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

            btn.addEventListener('keydown', function(e) {
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
        }

        document.querySelectorAll('.nav-group-toggle').forEach(wireGroup);

        document.querySelectorAll('.nav-dropdown').forEach(function(dropdown) {
            dropdown.addEventListener('keydown', function(e) {
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

        document.querySelectorAll('.note-more-menu').forEach(function(menu) {
            var btn = menu.querySelector('.btn-more');
            var dropdown = menu.querySelector('.note-more-dropdown');
            if (!btn || !dropdown) return;
            btn.addEventListener('click', function(e) {
                e.stopPropagation();
                var isOpen = btn.getAttribute('aria-expanded') === 'true';
                closeAllMoreMenus();
                btn.setAttribute('aria-expanded', String(!isOpen));
                dropdown.classList.toggle('open', !isOpen);
            });
            dropdown.addEventListener('keydown', function(e) {
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

        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape') {
                var menu = document.getElementById('nav-menu');
                if (menu && menu.classList.contains('open')) {
                    menu.classList.remove('open');
                    document.body.classList.remove('nav-open');
                    var scrim = document.getElementById('nav-scrim');
                    if (scrim) scrim.classList.remove('visible');
                    var hamburger = document.getElementById('nav-hamburger');
                    if (hamburger) {
                        hamburger.setAttribute('aria-expanded', 'false');
                        hamburger.setAttribute('aria-label', 'Open menu');
                        hamburger.focus();
                    }
                }
                closeAllGroups();
                closeAllMoreMenus();
            }
        });

        document.addEventListener('click', function(e) {
            if (!e.target.closest('.nav-group')) closeAllGroups();
            if (!e.target.closest('.note-more-menu')) closeAllMoreMenus();
        });
    });
})();
